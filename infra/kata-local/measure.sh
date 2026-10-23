#!/usr/bin/env bash
# Measures what kata.sh up built: /dev/kvm in the VM, the kernel a runc pod and a Kata pod see,
# a Kata pod's cold start and memory, how many fit at once, a PTY's first byte, and an
# agent-sandbox Sandbox started and suspended. Every line is a fact to read; nothing asserts.
# About 12 minutes. Results in out/measure.txt.
set -uo pipefail
cd "$(dirname "$0")"
eval "$(./kata.sh env)"
LIMACTL=$(command -v limactl || echo "$HOME/.local/bin/limactl")
RC=${RC:-kata-qemu}
mkdir -p out
OUT=out/measure.txt
: > "$OUT"
log() { printf '\n== %s\n' "$*" | tee -a "$OUT"; }
say() { echo "$*" | tee -a "$OUT"; }
in_vm() { "$LIMACTL" shell --workdir / kata-local -- "$@"; }
now_ms() { python3 -c 'import time; print(int(time.time() * 1000))'; }
mem_available() { in_vm awk '/MemAvailable/ { printf "%d", $2 / 1024 }' /proc/meminfo; }
pod_of() {
  local sel
  sel=$(kubectl get sandbox "$1" -o jsonpath='{.status.selector}' 2>/dev/null)
  [ -n "$sel" ] && kubectl get pod -l "$sel" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null
}

log "A. the VM"
say "$(in_vm sh -c 'ls -l /dev/kvm')"
say "$(in_vm sh -c 'sudo dmesg | grep -E "kvm \[1\]: (Hyp|nv:)" | sed "s/^\[[^]]*\] //"')"
say "VM: $(in_vm nproc) CPUs, $(in_vm awk '/MemTotal/ { printf "%d MiB", $2 / 1024 }' /proc/meminfo), kernel $(in_vm uname -r)"

log "B. cold start, image cached: apply to Ready, 5 times each"
for rc in runc "$RC"; do
  times=()
  for i in 1 2 3 4 5; do
    start=$(now_ms)
    kubectl run "cold-$i" --image=docker.io/library/alpine:3.22 --restart=Never \
      --overrides="{\"spec\":{\"terminationGracePeriodSeconds\":0$( [ "$rc" = runc ] || echo ",\"runtimeClassName\":\"$rc\"" )}}" \
      -- sleep infinity >/dev/null
    kubectl wait --for=condition=Ready "pod/cold-$i" --timeout=180s >/dev/null
    times+=("$(( $(now_ms) - start ))")
    kubectl delete pod "cold-$i" --wait=true >/dev/null
  done
  say "$rc: ${times[*]} ms"
done

log "C. memory of one Kata pod"
say "MemAvailable in the VM with no Kata pod: $(mem_available) MiB"
kubectl apply -f manifests/fill.yaml >/dev/null
kubectl scale deploy/kata-fill --replicas=1 >/dev/null
kubectl rollout status deploy/kata-fill --timeout=180s >/dev/null
sleep 10
say "$(in_vm sh -c 'for p in $(pgrep -x qemu-system-aar) $(pgrep -x virtiofsd) $(pgrep -f "^/opt/kata/bin/containerd-shim-kata-v2"); do
  printf "%-16s RSS %4d MiB, of it guest memory in /dev/shm %4d MiB\n" "$(cat /proc/$p/comm)" \
    "$(awk "/^VmRSS/ { print int(\$2 / 1024) }" /proc/$p/status)" "$(awk "/^RssShmem/ { print int(\$2 / 1024) }" /proc/$p/status)"
done')"
say "RuntimeClass $RC overhead: $(kubectl get runtimeclass "$RC" -o jsonpath='{.overhead.podFixed}')"

log "D. how many at once: scale in steps, 240 s for each step to come up"
for n in 4 8 12 16 20 24; do
  kubectl scale deploy/kata-fill --replicas=$n >/dev/null
  for _ in $(seq 1 48); do
    [ "$(kubectl get deploy kata-fill -o jsonpath='{.status.readyReplicas}')" = "$n" ] && break
    sleep 5
  done
  ready=$(kubectl get deploy kata-fill -o jsonpath='{.status.readyReplicas}')
  say "asked $n: ready ${ready:-0}, MemAvailable $(mem_available) MiB, /dev/shm used $(in_vm df -m --output=used /dev/shm | tail -1 | tr -d ' ') MiB"
  if [ "${ready:-0}" != "$n" ]; then
    kubectl get pod -l probe=fill --no-headers | awk '$3 != "Running" { print $1, $3 }' | head -3 | tee -a "$OUT"
    pending=$(kubectl get pod -l probe=fill --field-selector=status.phase=Pending -o name | head -1)
    [ -n "$pending" ] && kubectl get events --field-selector "involvedObject.name=${pending#pod/}" \
      -o custom-columns=R:.reason,M:.message --no-headers | tail -1 | cut -c1-240 | tee -a "$OUT"
    break
  fi
done
kubectl delete deploy kata-fill --wait=true >/dev/null
kubectl wait --for=delete pod -l probe=fill --timeout=180s >/dev/null 2>&1

log "E. kernels on the same node"
kubectl apply -f manifests/kernel.yaml >/dev/null
kubectl wait --for=condition=Ready pod -l probe=kernel --timeout=180s >/dev/null
for p in probe-runc probe-kata; do
  rc=$(kubectl get pod $p -o jsonpath='{.spec.runtimeClassName}')
  say "$p (${rc:-runc}, node $(kubectl get pod $p -o jsonpath='{.spec.nodeName}')): $(kubectl exec $p -- uname -r)"
done
say "probe-kata sees $(kubectl exec probe-kata -- nproc) CPU, $(kubectl exec probe-kata -- awk '/MemTotal/ { printf "%d MiB", $2 / 1024 }' /proc/meminfo)"

log "F. a PTY: kubectl exec -it, spawn to first byte, 5 times each"
for p in probe-runc probe-kata; do
  for i in 1 2 3 4 5; do
    say "$p: $(python3 first-byte.py kubectl exec -it $p -- sh -c 'tty; test -t 0 && echo isatty')"
  done
done
kubectl delete -f manifests/kernel.yaml --wait=false >/dev/null

log "G. agent-sandbox: a Sandbox in Kata, then suspend and resume"
start=$(now_ms)
kubectl apply -f manifests/sandbox.yaml >/dev/null
kubectl wait --for=condition=Ready sandbox/probe-issue --timeout=300s >/dev/null
say "ready after $(( $(now_ms) - start )) ms"
POD=$(pod_of probe-issue)
say "pod $POD, runtimeClassName $(kubectl get pod "$POD" -o jsonpath='{.spec.runtimeClassName}'), kernel $(kubectl exec "$POD" -- uname -r)"
kubectl exec "$POD" -- sh -c 'echo "written by $(hostname)" > /workspace/marker && cat /workspace/marker' | tee -a "$OUT"
start=$(now_ms)
kubectl patch sandbox probe-issue --type=merge -p '{"spec":{"operatingMode":"Suspended"}}' >/dev/null
for _ in $(seq 1 60); do [ -z "$(pod_of probe-issue)" ] && break; sleep 1; done
say "suspended after $(( $(now_ms) - start )) ms: pod $( [ -z "$(pod_of probe-issue)" ] && echo gone || echo 'still there'), workspace PVC $(kubectl get pvc -o jsonpath='{.items[*].status.phase}')"
kubectl get sandbox probe-issue -o jsonpath='{range .status.conditions[*]}{.type}={.status} ({.reason}){"\n"}{end}' | tee -a "$OUT"
start=$(now_ms)
kubectl patch sandbox probe-issue --type=merge -p '{"spec":{"operatingMode":"Running"}}' >/dev/null
kubectl wait --for=condition=Ready sandbox/probe-issue --timeout=300s >/dev/null
POD=$(pod_of probe-issue)
say "resumed after $(( $(now_ms) - start )) ms, pod $POD"
say "marker after resume: $(kubectl exec "$POD" -- cat /workspace/marker 2>&1)"
kubectl delete -f manifests/sandbox.yaml --wait=true >/dev/null
kubectl delete pvc --all --wait=false >/dev/null

say ""
say "done — out/measure.txt"
