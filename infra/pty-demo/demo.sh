#!/usr/bin/env bash
# The talk's runbook in one file.
#
#   ./demo.sh up [--no-vm] [--no-kata]   create everything the backends attach to (idempotent)
#   ./demo.sh serve                      the page, on http://127.0.0.1:7681
#   ./demo.sh status                     what exists right now
#   ./demo.sh down                       remove all of it, the Azure VM and the Kata VM included
#
# `up` stops once, for a person: `sbx login` on the VM is a browser device-code flow, and the
# session dies with the VM, so every fresh VM needs it.
set -euo pipefail
cd "$(dirname "$0")"
HERE=$PWD
VMDIR=$HERE/../sbx-vm
KATA_LOCAL=$HERE/../kata-local
CTX=kind-sandbox-demo
KIND=$(command -v kind || echo "$HOME/.local/bin/kind")
WORKSPACE=${DEMO_WORKSPACE:-$HOME/projects/charlas/ai-orchestator-new}
AGENT_SANDBOX=v1.0.2

step() { printf '\n\033[1m▸ %s\033[0m\n' "$*"; }
vm_ip() { terraform -chdir="$VMDIR" output -raw public_ip 2>/dev/null || true; }
on_vm() { ssh -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 "azureuser@$(vm_ip)" "$@"; }

up_local_sbx() {
  step "2 · sbx on this machine"
  sbx ls 2>/dev/null | grep -q '^pty-demo ' || sbx create --name pty-demo shell "$WORKSPACE" | tail -1
  sbx ls | grep '^pty-demo '
}

up_kata() {
  step "4 · Kata on k3s in a Lima VM + agent-sandbox"
  "$KATA_LOCAL/kata.sh" up | grep -E '/dev/kvm|^daemon set|successfully rolled out' || true
  local kc
  kc=$("$KATA_LOCAL/kata.sh" env | cut -d= -f2)
  kubectl --kubeconfig "$kc" apply -f sandbox-kata.yaml >/dev/null
  # The pod VM boots on the first create, about 30 s; the stage should not wait for it.
  kubectl --kubeconfig "$kc" wait --for=condition=Ready sandbox/pty-demo --timeout=300s
}

up_kind() {
  step "4b · kind + agent-sandbox $AGENT_SANDBOX"
  "$KIND" get clusters 2>/dev/null | grep -qx sandbox-demo || "$KIND" create cluster --name sandbox-demo --wait 120s
  kubectl --context $CTX apply -f "https://github.com/kubernetes-sigs/agent-sandbox/releases/download/$AGENT_SANDBOX/sandbox-with-extensions.yaml" >/dev/null
  kubectl --context $CTX -n agent-sandbox-system rollout status deploy --timeout=180s | tail -1
  kubectl --context $CTX apply -f sandbox.yaml >/dev/null
  kubectl --context $CTX wait --for=condition=Ready sandbox/pty-demo --timeout=180s
}

up_vm() {
  step "3 · the Azure VM"
  local ip
  ip=$(curl -fsS https://api.ipify.org)
  # The NSG lets SSH in from one address. At a venue that address is not the one from rehearsal,
  # so it is rewritten on every `up` and Terraform updates the rule in place.
  [ -f "$VMDIR/terraform.tfvars" ] || cp "$VMDIR/terraform.tfvars.example" "$VMDIR/terraform.tfvars"
  sed -i.bak "s#^ssh_source_cidr .*#ssh_source_cidr     = \"$ip/32\"#" "$VMDIR/terraform.tfvars" && rm -f "$VMDIR/terraform.tfvars.bak"
  terraform -chdir="$VMDIR" init -input=false >/dev/null
  terraform -chdir="$VMDIR" apply -input=false -auto-approve | grep -E "Apply complete|Error" || true
  echo "VM $(vm_ip), SSH allowed from $ip"

  for _ in $(seq 1 60); do on_vm test -f /var/lib/cloud/sbx-ready 2>/dev/null && break; sleep 10; done
  on_vm sbx version

  # `sbx ls` may answer without a session; the 401 is what says there is none.
  signed_in() { ! on_vm 'sbx ls 2>&1' | grep -qiE "not authenticated|sign in"; }
  if ! signed_in; then
    printf '\n\033[33mSign in to Docker on the VM, in another terminal:\033[0m\n\n  ssh -t azureuser@%s sbx login\n\nWaiting…\n' "$(vm_ip)"
    until signed_in; do sleep 5; done
  fi

  on_vm 'sbx policy init balanced >/dev/null 2>&1 || true
         mkdir -p ~/workspace && cd ~/workspace && { [ -d .git ] || { git init -q && echo "# demo" > README.md; }; }
         sbx ls | grep -q "^pty-demo " || sbx create --name pty-demo shell /home/azureuser/workspace | tail -1
         sbx ls | grep "^pty-demo "'
  # The first exec into a fresh sandbox took 6.8 s; warm it so the stage does not wait.
  on_vm sbx exec pty-demo true
}

up_aca() {
  step "5 · ACA Sandboxes"
  export ACA_SUBSCRIPTION=${ACA_SUBSCRIPTION:?set ACA_SUBSCRIPTION to your Azure subscription id}
  export ACA_RESOURCE_GROUP=${ACA_RESOURCE_GROUP:-netcoreconf-dev-rg-01}
  export ACA_SANDBOX_GROUP=${ACA_SANDBOX_GROUP:-ptydemo} ACA_REGION=${ACA_REGION:-northeurope}
  aca sandboxgroup list 2>/dev/null | grep -q " $ACA_SANDBOX_GROUP " ||
    aca sandboxgroup create --name "$ACA_SANDBOX_GROUP" --location "$ACA_REGION" | tail -1
  if ! aca sandbox list >/dev/null 2>&1; then
    echo "no data-plane access to $ACA_SANDBOX_GROUP — backend 5 stays disabled (needs Container Apps SandboxGroup Data Owner)"
    return 0
  fi
  aca sandbox list -o json 2>/dev/null | grep -q '"pty-demo"' || aca sandbox create --disk ubuntu --label name=pty-demo | tail -1
  # It stops by itself when idle, and a stopped sandbox does not open a shell.
  aca sandbox resume -l name=pty-demo >/dev/null 2>&1 || true
}

case "${1:-}" in
  up)
    shift
    [ -d node_modules ] || npm install --no-audit --no-fund --silent
    up_local_sbx
    case " $* " in *" --no-kata "*) ;; *) up_kata ;; esac
    up_kind
    up_aca
    case " $* " in *" --no-vm "*) ;; *) up_vm ;; esac
    step "ready — ./demo.sh serve"
    ;;
  serve)
    exec node server.js
    ;;
  status)
    step "sbx (local)"; sbx ls 2>/dev/null | grep '^pty-demo ' || echo "none"
    step "Kata"; kubectl --kubeconfig "$("$KATA_LOCAL/kata.sh" env 2>/dev/null | cut -d= -f2)" get sandbox pty-demo 2>/dev/null || echo "none"
    step "kind"; kubectl --context $CTX get sandbox pty-demo 2>/dev/null || echo "none"
    step "VM"; [ -n "$(vm_ip)" ] && echo "azureuser@$(vm_ip)" || echo "none"
    ;;
  down)
    step "sbx (local)"; sbx rm --force pty-demo 2>/dev/null || true
    "$KATA_LOCAL/kata.sh" down
    step "kind"; "$KIND" delete cluster --name sandbox-demo 2>/dev/null || true
    step "ACA"; ACA_SUBSCRIPTION=${ACA_SUBSCRIPTION:?set ACA_SUBSCRIPTION to your Azure subscription id} ACA_RESOURCE_GROUP=${ACA_RESOURCE_GROUP:-netcoreconf-dev-rg-01} \
      ACA_SANDBOX_GROUP=${ACA_SANDBOX_GROUP:-ptydemo} ACA_REGION=${ACA_REGION:-northeurope} aca sandbox delete -l name=pty-demo --yes 2>/dev/null || true
    step "VM"; terraform -chdir="$VMDIR" destroy -input=false -auto-approve | grep -E "Destroy complete|Error" || true
    ;;
  *)
    sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
