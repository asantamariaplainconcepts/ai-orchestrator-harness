/**
 * The one HTTP entry point. Paths are relative because the SPA is served same-origin by the
 * host in every habitat — there is no API base URL to configure, and no CORS.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    /**
     * The `detail` from the API's problem response, when it sent one. Carried because some
     * refusals are the answer — "OpenCode is not signed in, run `opencode auth login`" is useless
     * if the screen replaces it with "something went wrong".
     */
    readonly detail?: string,
    /**
     * The domain error's `code` — `PullRequest.Stale` — when the problem carried one. #419. A
     * screen that must answer two refusals with one status differently (a stale draft and a pending
     * review are both 409) reads this rather than matching the sentence.
     */
    readonly code?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * The API answers failures with RFC 7807; a body that is not one simply yields nothing.
 *
 * Two shapes, because the API emits two: a plain problem carries `detail`, while a validation
 * problem carries no detail at all and puts the messages under `errors`, keyed by field. Reading
 * only `detail` would throw away exactly the refusals that name what to fix.
 */
async function problemCode(response: Response): Promise<string | undefined> {
  try {
    const body: unknown = await response.clone().json();
    if (!body || typeof body !== "object") return undefined;
    const { code } = body as { code?: unknown };
    return typeof code === "string" && code.length > 0 ? code : undefined;
  } catch {
    return undefined;
  }
}

async function problemDetail(response: Response): Promise<string | undefined> {
  try {
    const body: unknown = await response.clone().json();
    if (!body || typeof body !== "object") return undefined;

    const { detail, errors } = body as { detail?: unknown; errors?: unknown };
    if (typeof detail === "string" && detail.length > 0) return detail;

    if (errors && typeof errors === "object") {
      const messages = Object.values(errors as Record<string, unknown>)
        .flatMap((value) => (Array.isArray(value) ? value : [value]))
        .filter((value): value is string => typeof value === "string" && value.length > 0);
      if (messages.length > 0) return messages.join(" ");
    }

    return undefined;
  } catch {
    return undefined;
  }
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const response = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!response.ok) {
    throw new ApiError(
      `Request to ${path} failed`,
      response.status,
      await problemDetail(response),
      await problemCode(response),
    );
  }

  return response.status === 204
    ? (undefined as TResponse)
    : ((await response.json()) as TResponse);
}

export const api = {
  get: <TResponse>(path: string, init?: RequestInit) => request<TResponse>(path, init),
  post: <TResponse>(path: string, body?: unknown) =>
    request<TResponse>(path, { method: "POST", body: JSON.stringify(body ?? {}) }),
  put: <TResponse>(path: string, body: unknown) =>
    request<TResponse>(path, { method: "PUT", body: JSON.stringify(body) }),
  delete: <TResponse>(path: string) => request<TResponse>(path, { method: "DELETE" }),
};

/** What the screen shows when a request fails. Prefers the API's own sentence. */
export function explain(error: unknown): string {
  if (error instanceof ApiError) {
    return error.detail ?? "The request was refused.";
  }
  return "Could not reach the server.";
}
