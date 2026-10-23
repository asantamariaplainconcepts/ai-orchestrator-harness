#!/usr/bin/env bash
# agent-sandbox (kubernetes-sigs, v1.0.2) on the Kata pool: a cold Sandbox with a persistent
# workspace, suspend and resume, a warm pool and a claim, and a terminal attached the way the
# harness would attach one. Every line is a fact to read; nothing asserts.
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p out
OUT=out/agent-sandbox.txt
: > "$OUT"
log() { printf '\n== %s\n' "$*" | tee -a "$OUT"; }
say() { echo "$*" | tee -a "$OUT"; }
VERSION=v1.0.2

# The pod behind a Sandbox, found by the selector the controller publishes rather than by
# guessing its name.
pod_of() {
  local sel
  sel=$(kubectl get sandbox "$1" -o jsonpath='{.status.selector}' 2>/dev/null)
  [ -n "$sel" ] && kubectl get pod -l "$sel" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null
}
ready() { kubectl wait --for=condition=Ready "$@" --timeout=300s >/dev/null 2>&1; }

log "install agent-sandbox $VERSION"
kubectl apply -f "https://github.com/kubernetes-sigs/agent-sandbox/releases/download/$VERSION/sandbox-with-extensions.yaml" >/dev/null
kubectl -n agent-sandbox-system rollout status deploy --timeout=180s 2>&1 | tail -1 | tee -a "$OUT"
kubectl get crd -o name | grep agents.x-k8s.io | tee -a "$OUT"

# --- A. Cold start: a Sandbox with its own disk --------------------------------------------
log "A. cold Sandbox, Kata, 4Gi workspace"
start=$(date +%s)
kubectl apply -f agent-sandbox/sandbox.yaml >/dev/null
ready sandbox/probe-issue
say "ready after $(( $(date +%s) - start ))s (pod VM boot + Azure Disk attach)"
POD=$(pod_of probe-issue)
say "pod: $POD on $(kubectl get pod "$POD" -o jsonpath='{.spec.nodeName}')"
say "kernel: $(kubectl exec "$POD" -- uname -r)"
kubectl exec "$POD" -- sh -c 'echo "written $(date -u +%FT%TZ) by $(hostname)" > /workspace/marker && cat /workspace/marker' | tee -a "$OUT"

# --- B. Suspend and resume: what a hold would keep -----------------------------------------
log "B. suspend, then resume"
kubectl patch sandbox probe-issue --type=merge -p '{"spec":{"operatingMode":"Suspended"}}' >/dev/null
for i in $(seq 1 60); do [ -z "$(pod_of probe-issue)" ] && break; sleep 2; done
say "suspended: pod is $( [ -z "$(pod_of probe-issue)" ] && echo gone || echo 'still there')"
say "pvc kept: $(kubectl get pvc -o name | grep -c workspace)"
start=$(date +%s)
kubectl patch sandbox probe-issue --type=merge -p '{"spec":{"operatingMode":"Running"}}' >/dev/null
ready sandbox/probe-issue
POD=$(pod_of probe-issue)
say "resumed after $(( $(date +%s) - start ))s, pod: $POD"
say "marker after resume: $(kubectl exec "$POD" -- cat /workspace/marker 2>&1)"
say "same hostname: $(kubectl exec "$POD" -- hostname)"

# --- C. Warm pool and claim ----------------------------------------------------------------
log "C. warm pool of 2, then a claim"
start=$(date +%s)
kubectl apply -f agent-sandbox/pool.yaml >/dev/null
for i in $(seq 1 150); do
  [ "$(kubectl get sandboxwarmpool agent-kata -o jsonpath='{.status.readyReplicas}' 2>/dev/null)" = "2" ] && break
  sleep 2
done
say "pool ready ($(kubectl get sandboxwarmpool agent-kata -o jsonpath='{.status.readyReplicas}')/2) after $(( $(date +%s) - start ))s"
start=$(date +%s)
kubectl apply -f agent-sandbox/claim.yaml >/dev/null
ready sandboxclaim/probe-run
CLAIMED=$(kubectl get sandboxclaim probe-run -o jsonpath='{.status.sandbox.name}')
say "claim ready after $(( $(date +%s) - start ))s -> sandbox $CLAIMED"
say "claimed kernel: $(kubectl exec "$(pod_of "$CLAIMED")" -- uname -r)"
sleep 10
say "pool refilled to: $(kubectl get sandboxwarmpool agent-kata -o jsonpath='{.status.readyReplicas}')"

# --- D. A terminal, the way the harness would hold one ------------------------------------
# The harness spawns a command in a PTY and streams it to the browser. For a cloud sandbox that
# command is `kubectl exec -it`; `script` gives it a real PTY here, as PtyAgentRunner would.
log "D. a PTY into the Kata sandbox"
POD=$(pod_of probe-issue)
script -q /dev/null kubectl exec -it "$POD" -- sh -c 'tty; stty size; echo TERM=$TERM' 2>&1 | tr -d '\r' | tee -a "$OUT"

log "E. what the cluster shows"
kubectl get sandboxes,sandboxclaims,sandboxwarmpools 2>&1 | tee -a "$OUT"

log "cleanup"
kubectl delete -f agent-sandbox/claim.yaml -f agent-sandbox/pool.yaml -f agent-sandbox/sandbox.yaml --wait=false >/dev/null
say "done — results in tests/out/agent-sandbox.txt"
