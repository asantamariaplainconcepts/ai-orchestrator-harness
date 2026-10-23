#!/usr/bin/env bash
# Runs the three probes against the cluster in the current kubectl context and writes what each
# measured to tests/out/. Nothing here asserts; every line is a fact to read.
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p out
log() { printf '\n== %s\n' "$*" | tee -a out/results.txt; }
: > out/results.txt

log "context: $(kubectl config current-context)"
kubectl get runtimeclass 2>&1 | tee -a out/results.txt
kubectl get nodes -L probe/pool,kubernetes.azure.com/os-sku 2>&1 | tee -a out/results.txt

# --- 1. Kernel isolation, and what a Kata pod costs to start --------------------------------
log "1. kernel: runc vs kata"
start=$(date +%s)
kubectl apply -f kernel.yaml >/dev/null
kubectl wait --for=condition=Ready pod/probe-runc --timeout=300s >/dev/null
echo "runc ready after $(( $(date +%s) - start ))s" | tee -a out/results.txt
kubectl wait --for=condition=Ready pod/probe-kata --timeout=300s >/dev/null
echo "kata ready after $(( $(date +%s) - start ))s (image already pulled by runc)" | tee -a out/results.txt
echo "runc kernel: $(kubectl exec probe-runc -- uname -r)" | tee -a out/results.txt
echo "kata kernel: $(kubectl exec probe-kata -- uname -r)" | tee -a out/results.txt
echo "kata nproc/mem: $(kubectl exec probe-kata -- sh -c 'nproc; grep MemTotal /proc/meminfo')" | tee -a out/results.txt

# --- 2. Docker inside a Kata pod -----------------------------------------------------------
log "2. dind in kata"
start=$(date +%s)
kubectl apply -f dind-kata.yaml >/dev/null
if kubectl wait --for=condition=Ready pod/probe-dind --timeout=420s >/dev/null; then
  echo "dind pod ready after $(( $(date +%s) - start ))s" | tee -a out/results.txt
  for i in $(seq 1 30); do
    kubectl exec probe-dind -c dockerd -- docker -H tcp://127.0.0.1:2375 info >/dev/null 2>&1 && break
    sleep 2
  done
  kubectl exec probe-dind -c dockerd -- docker -H tcp://127.0.0.1:2375 info --format 'dockerd {{.ServerVersion}} storage={{.Driver}} kernel={{.KernelVersion}}' 2>&1 | tee -a out/results.txt
  kubectl exec probe-dind -c dockerd -- docker -H tcp://127.0.0.1:2375 run --rm alpine uname -r 2>&1 | sed 's/^/container-in-kata kernel: /' | tee -a out/results.txt
else
  echo "dind pod NOT ready" | tee -a out/results.txt
  kubectl describe pod probe-dind | tail -25 | tee -a out/results.txt
  kubectl logs probe-dind -c dockerd --tail=40 2>&1 | tee -a out/results.txt
fi

# --- 3. Testcontainers against the daemon in the same pod VM -------------------------------
log "3. testcontainers in kata"
if kubectl get pod probe-dind -o jsonpath='{.status.phase}' 2>/dev/null | grep -q Running; then
  kubectl exec probe-dind -c sdk -- mkdir -p /work >/dev/null
  kubectl cp ../../tc-probe/. probe-dind:/work -c sdk
  start=$(date +%s)
  kubectl exec probe-dind -c sdk -- sh -c 'cd /work && dotnet test --logger "console;verbosity=detailed" 2>&1' > out/testcontainers.log
  echo "dotnet test took $(( $(date +%s) - start ))s (restore + build + test)" | tee -a out/results.txt
  grep -E "PG:|Passed!|Failed!|Error Message" -A1 out/testcontainers.log | tee -a out/results.txt
fi

log "done — full Testcontainers log in tests/out/testcontainers.log"
