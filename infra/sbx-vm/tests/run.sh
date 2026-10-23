#!/usr/bin/env bash
# The probe that ran on the Mac, run on the VM: sbx up, a sandbox with its own Docker daemon, and
# the Testcontainers Postgres round-trip inside it. Every line is a fact to read, not an assertion.
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p out
: > out/results.txt
log() { printf '\n== %s\n' "$*" | tee -a out/results.txt; }

IP=$(terraform -chdir=.. output -raw public_ip)
SSH="ssh -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 azureuser@$IP"
on() { $SSH "$@" 2>&1 | tee -a out/results.txt; }

log "waiting for cloud-init on $IP"
for i in $(seq 1 60); do $SSH test -f /var/lib/cloud/sbx-ready 2>/dev/null && break; sleep 10; done

log "1. the VM can host microVMs"
on 'cat /var/log/kvm-ok.log; ls -l /dev/kvm; nproc; free -g | sed -n 2p'
on 'sbx version; docker version --format "host docker {{.Server.Version}}"'

log "2. a sandbox, and the daemon inside it"
on 'sbx policy init balanced 2>&1 | tail -1'
on 'mkdir -p ~/tc-probe'
scp -q -o StrictHostKeyChecking=accept-new ../../tc-probe/*.cs* "azureuser@$IP:tc-probe/"
start=$(date +%s)
on 'sbx create --name tc-probe shell /home/azureuser/tc-probe 2>&1 | tail -2'
echo "sandbox created in $(( $(date +%s) - start ))s" | tee -a out/results.txt
on 'sbx exec tc-probe sh -c "uname -r; docker info --format \"sandbox docker {{.ServerVersion}} on {{.OperatingSystem}}\""'

log "3. testcontainers inside the sandbox"
on 'sbx policy allow network --sandbox tc-probe "dot.net,builds.dotnet.microsoft.com,*.dotnet.microsoft.com,dotnetcli.azureedge.net,dotnetbuilds.azureedge.net,api.nuget.org,*.nuget.org" 2>&1 | tail -1'
on 'sbx exec tc-probe sh -c "curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir \$HOME/.dotnet >/dev/null && \$HOME/.dotnet/dotnet --version"'
start=$(date +%s)
$SSH 'sbx exec tc-probe sh -c "cd /home/azureuser/tc-probe && DOTNET_CLI_TELEMETRY_OPTOUT=1 \$HOME/.dotnet/dotnet test --logger \"console;verbosity=detailed\" 2>&1"' > out/testcontainers.log 2>&1
echo "dotnet test took $(( $(date +%s) - start ))s (restore + build + test)" | tee -a out/results.txt
grep -E "PG:|Passed!|Failed!|Error Message" -A1 out/testcontainers.log | tee -a out/results.txt

log "done — full log in tests/out/testcontainers.log"
