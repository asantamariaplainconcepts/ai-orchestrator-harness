#!/usr/bin/env bash
# Kubernetes with Kata Containers on this Mac: k3s in a Lima VM with nested virtualization, one
# microVM per pod.
#
#   ./kata.sh up       the VM, k3s, Kata and agent-sandbox (idempotent)
#   ./kata.sh status   what exists right now
#   ./kata.sh env      the KUBECONFIG line for this cluster, for eval
#   ./kata.sh down     delete the VM and its disks
#
# Needs an Apple M3 or later on macOS 15 or later, and limactl on the PATH or in ~/.local/bin.
set -euo pipefail
cd "$(dirname "$0")"
VM=kata-local
KATA=4.2.0
AGENT_SANDBOX=v1.0.2
LIMACTL=$(command -v limactl || echo "$HOME/.local/bin/limactl")

step() { printf '\n\033[1m▸ %s\033[0m\n' "$*"; }
exists() { "$LIMACTL" list --format '{{.Name}}' 2>/dev/null | grep -qx "$VM"; }
running() { [ "$("$LIMACTL" list "$VM" --format '{{.Status}}' 2>/dev/null)" = Running ]; }
in_vm() { "$LIMACTL" shell --workdir / "$VM" -- "$@"; }
# The kubeconfig lives in the VM's own directory, so `down` takes it with the VM.
kubeconfig() { echo "$("$LIMACTL" list "$VM" --format '{{.Dir}}')/kubeconfig.yaml"; }
k() { kubectl --kubeconfig "$(kubeconfig)" "$@"; }

up() {
  [ -x "$LIMACTL" ] || { echo "limactl not found: install Lima (brew install lima, or the release binary into ~/.local/bin)"; exit 1; }

  step "VM $VM: 4 CPUs, 8 GiB, 40 GiB, nested virtualization"
  if ! exists; then
    "$LIMACTL" start --name "$VM" --tty=false ./lima.yaml 2>&1 | grep -E 'READY|level=(error|fatal)' || true
  elif ! running; then
    "$LIMACTL" start --tty=false "$VM" 2>&1 | grep -E 'READY|level=(error|fatal)' || true
  fi
  running || { echo "the VM did not start: limactl start $VM shows why"; exit 1; }
  # Without /dev/kvm Kata has nothing to boot its VMs with, and nothing after this would work.
  in_vm test -c /dev/kvm || { echo "no /dev/kvm in the VM: nested virtualization needs an M3 or later and macOS 15 or later"; exit 1; }
  echo "/dev/kvm present, kernel $(in_vm uname -r)"

  in_vm sudo sed 's#127.0.0.1:6443#127.0.0.1:16443#; s#: default$#: kata-local#' /etc/rancher/k3s/k3s.yaml > "$(kubeconfig)"
  k wait --for=condition=Ready node --all --timeout=180s | tail -1

  step "Kata Containers $KATA (kata-deploy)"
  # Only the two QEMU shims: on arm64, kata-deploy $KATA ships neither Cloud Hypervisor nor
  # Firecracker, and their RuntimeClasses would exist with no handler behind them.
  in_vm sh -c "KUBECONFIG=/etc/rancher/k3s/k3s.yaml helm upgrade --install kata-deploy \
    oci://ghcr.io/kata-containers/kata-deploy-charts/kata-deploy --version $KATA -n kube-system \
    --set k8sDistribution=k3s --set 'snapshotter.setup={}' --set shims.disableAll=true \
    --set shims.qemu.enabled=true --set shims.qemu-runtime-rs.enabled=true --wait --timeout 10m" | grep -E '^STATUS' || true
  k -n kube-system rollout status ds/kata-deploy --timeout=600s 2>/dev/null | tail -1
  for _ in $(seq 1 60); do in_vm test -x /opt/kata/bin/containerd-shim-kata-v2 && break; sleep 5; done
  # What nested virtualization needs, as drop-ins: kata-nested.toml says what and why.
  "$LIMACTL" copy kata-nested.toml kata-nested-rs.toml "$VM:/tmp/"
  in_vm sudo sh -c 'd=/opt/kata/share/defaults/kata-containers
    install -m 0644 /tmp/kata-nested.toml $d/runtimes/qemu/config.d/90-nested.toml
    install -m 0644 /tmp/kata-nested.toml $d/runtime-rs/runtimes/qemu-runtime-rs/config.d/90-nested.toml
    install -m 0644 /tmp/kata-nested-rs.toml $d/runtime-rs/runtimes/qemu-runtime-rs/config.d/91-nested-rs.toml'
  k get runtimeclass -o name | tr '\n' ' '; echo

  step "agent-sandbox $AGENT_SANDBOX"
  k apply -f "https://github.com/kubernetes-sigs/agent-sandbox/releases/download/$AGENT_SANDBOX/sandbox-with-extensions.yaml" >/dev/null
  k -n agent-sandbox-system rollout status deploy --timeout=180s | tail -1

  step "ready — eval \"\$(./kata.sh env)\""
}

case "${1:-}" in
  up) up ;;
  env)
    exists || { echo "no VM: ./kata.sh up" >&2; exit 1; }
    echo "export KUBECONFIG=$(kubeconfig)"
    ;;
  status)
    step "VM"; exists && "$LIMACTL" list "$VM" || echo "none"
    if exists && running; then
      step "node"; k get node -o wide 2>/dev/null || echo "API not answering on 127.0.0.1:16443"
      step "Kata"; k get runtimeclass kata-qemu kata-qemu-runtime-rs 2>/dev/null || echo "not installed"
      step "agent-sandbox"; k get sandboxes -A 2>/dev/null || echo "not installed"
    fi
    ;;
  down)
    step "VM $VM and its disks"
    if exists; then "$LIMACTL" delete --force "$VM" 2>&1 | grep -E 'level=(error|fatal)' || true; fi
    if exists; then echo "still there: limactl delete --force $VM"; exit 1; fi
    echo "deleted"
    ;;
  *)
    sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
