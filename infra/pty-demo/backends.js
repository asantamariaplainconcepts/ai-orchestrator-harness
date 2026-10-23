// Every backend is the same thing: a command a PTY can spawn. The terminal in the browser does not
// know which one it got — that is the property the harness's IAgentRunner seam would have, shown
// with nothing else around it.
//
// `probe` is what each one runs before handing over an interactive shell: who am I, which kernel,
// is there a Docker daemon of my own. The kernel line is the one that changes between backends.
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { fileURLToPath } from "node:url";

const facts = [
  'printf "\\033[1;36m── %s\\033[0m\\n" "$(hostname)"',
  'printf "kernel   %s\\n" "$(uname -r)"',
  'printf "user     %s\\n" "$(id -un)"',
  'printf "cwd      %s\\n" "$(pwd)"',
  'printf "docker   %s\\n" "$(docker info --format "{{.ServerVersion}} on {{.OperatingSystem}}" 2>/dev/null || echo none)"',
  'printf "tty      %s  %s\\n" "$(tty)" "$(stty size 2>/dev/null)"',
  "echo",
];

// The host keeps its own login zsh and prompt. Inside a sandbox there is no SHELL, and a login
// shell's profile overwrites PS1 with a bare "$ ", so the sandboxes get an interactive sh with a
// prompt that says where it is. dash and busybox ash print PS1 literally, so the colour is made
// by printf rather than written as \033.
const probe = (shell) =>
  [...facts, ...(shell === "host" ? ["exec /bin/zsh -l"] : ['export PS1="$(printf "\\033[1;32m")$(id -un)@$(hostname)$(printf "\\033[0m") $ "', "exec /bin/sh -i"])].join("; ");

// A string as one shell word, for lines the backends above splice into `sh -c`.
const shq = (text) => `'${text.replace(/'/g, `'\\''`)}'`;

const workspace = process.env.DEMO_WORKSPACE ?? `${homedir()}/projects/charlas/ai-orchestator-new`;

// The VM's address comes from infra/sbx-vm's own state, so a fresh `terraform apply` needs no
// edit here. DEMO_VM overrides it; no state means backend 3 is shown disabled.
function vmTarget() {
  if (process.env.DEMO_VM) return process.env.DEMO_VM;
  try {
    const dir = fileURLToPath(new URL("../sbx-vm", import.meta.url));
    const ip = execFileSync("terraform", [`-chdir=${dir}`, "output", "-raw", "public_ip"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
    return /^\d+\.\d+\.\d+\.\d+$/.test(ip) ? `azureuser@${ip}` : null;
  } catch {
    return null;
  }
}
const vm = vmTarget();

// ACA Sandboxes answer only to an identity holding "Container Apps SandboxGroup Data Owner" on the
// group; Contributor on the resource group creates the group and is then refused every data-plane
// call. So the backend is offered only when a list call succeeds — asked once, at startup.
const aca = {
  ACA_SUBSCRIPTION: process.env.ACA_SUBSCRIPTION ?? "",
  ACA_RESOURCE_GROUP: process.env.ACA_RESOURCE_GROUP ?? "netcoreconf-dev-rg-01",
  ACA_SANDBOX_GROUP: process.env.ACA_SANDBOX_GROUP ?? "ptydemo",
  ACA_REGION: process.env.ACA_REGION ?? "northeurope",
};
function acaReachable() {
  try {
    execFileSync("aca", ["sandbox", "list"], { env: { ...process.env, ...aca }, stdio: "ignore", timeout: 20000 });
    return true;
  } catch {
    return false;
  }
}

// Backends 4 and 4b are the same agent-sandbox Sandbox, found by the selector its controller
// publishes, on two clusters: k3s with Kata in a Lima VM, and kind under runc.
const sandboxShell = `POD=$(kubectl --context "$CTX" get pod -l "$(kubectl --context "$CTX" get sandbox pty-demo -o jsonpath='{.status.selector}')" -o jsonpath='{.items[0].metadata.name}') && exec kubectl --context "$CTX" exec -it "$POD" -- sh -c '${probe("sandbox")}'`;

// infra/kata-local writes its kubeconfig into the Lima VM's directory, so it exists exactly while
// the VM does; no file means backend 4 is shown disabled.
const kataKubeconfig = `${process.env.LIMA_HOME ?? `${homedir()}/.lima`}/kata-local/kubeconfig.yaml`;

export const backends = {
  local: {
    label: "1 · Local (worktree en el host)",
    isolation: "ninguna: tu usuario, tu kernel, tu ~",
    command: "/bin/zsh",
    args: ["-c", probe("host")],
    cwd: workspace,
  },
  sbx: {
    label: "2 · Docker sbx (microVM en el portátil)",
    isolation: "microVM con kernel propio y dockerd propio",
    command: "sbx",
    args: ["exec", "-it", "pty-demo", "sh", "-c", probe("sandbox")],
  },
  vm: {
    label: "3 · sbx en una VM de Azure",
    isolation: vm ? `microVM dentro de una VM de tu suscripción (${vm.split("@")[1]})` : "—",
    command: "ssh",
    args: ["-tt", "-o", "StrictHostKeyChecking=accept-new", vm ?? "", "sbx", "exec", "-it", "pty-demo", "sh", "-c", `'${probe("sandbox")}'`],
    disabled: !vm && "no hay VM: ./demo.sh up",
  },
  kata: {
    label: "4 · agent-sandbox en Kata (portátil)",
    isolation: "k3s en una VM Lima con virtualización anidada: cada pod en su microVM, con su kernel",
    command: "sh",
    args: ["-c", sandboxShell],
    env: { CTX: "kata-local", KUBECONFIG: kataKubeconfig },
    disabled: !existsSync(kataKubeconfig) && "no hay clúster Kata: ../kata-local/kata.sh up",
  },
  k8s: {
    label: "4b · agent-sandbox en kind (runc)",
    isolation: process.env.DEMO_K8S_ISOLATION ?? "kind en el portátil: runc, el kernel compartido — el plan B si no hay Kata",
    command: "sh",
    args: ["-c", sandboxShell],
    env: { CTX: process.env.DEMO_K8S_CONTEXT ?? "kind-sandbox-demo" },
  },
  aca: {
    label: "5 · ACA Sandboxes (Azure, gestionado)",
    isolation: `sandbox gestionado en ${aca.ACA_REGION}: sin VM ni cluster que mantener, sin Docker dentro`,
    // A stopped sandbox does not open a shell, and this one stopped by itself minutes after the
    // last connection; resume is a no-op on a running one, so every connect starts with it.
    // `aca sandbox shell -c` takes an executable, not a shell line, so the probe is written into the
    // sandbox as a script and that script is what the PTY runs. It is rewritten on every connect:
    // the file is a few hundred bytes and the sandbox may have been recreated since the last one.
    command: "sh",
    args: [
      "-c",
      `aca sandbox resume -l name=pty-demo >/dev/null 2>&1; printf '%s\\n' '#!/bin/sh' ${shq(probe("sandbox"))} > "$PROBE" && aca sandbox fs write -l name=pty-demo --path /root/probe.sh --file "$PROBE" >/dev/null && aca sandbox exec -l name=pty-demo -c 'chmod +x /root/probe.sh' >/dev/null && exec aca sandbox shell -l name=pty-demo -c /root/probe.sh`,
    ],
    env: { ...aca, PROBE: "/tmp/pty-demo-aca-probe.sh" },
    disabled: !acaReachable() && `sin acceso al data plane de ${aca.ACA_SANDBOX_GROUP}: falta el rol Container Apps SandboxGroup Data Owner`,
  },
};
