# infra — sandbox probes

Throwaway environments that answer one question: where can a Run execute, isolated from the
machine, when its gate needs a Docker daemon — and can a person still watch it? None of it is wired
into the product. It exists to be created, measured and destroyed — for the talk, and as evidence
for whichever seam comes next behind `IAgentRunner`.

| Folder | What it builds | Status in Labsplain |
| --- | --- | --- |
| [`sbx-vm/`](sbx-vm/) | One Ubuntu VM running Docker Sandboxes (`sbx`) | **Works**, measured 2026-10-01 |
| [`aks-kata/`](aks-kata/) | AKS with a Pod Sandboxing (Kata) node pool | **Blocked** by the tenant's naming policy |
| [`tc-probe/`](tc-probe/) | A one-test Testcontainers project: start Postgres 16, `select version()` | Shared by both |
| [`pty-demo/`](pty-demo/) | One browser terminal, six backends: host, sbx, sbx on the VM, agent-sandbox in Kata and in kind, ACA Sandboxes | **Works**, the talk's demo |
| [`kata-local/`](kata-local/) | k3s with Kata Containers in a Lima VM on this Mac, nested virtualization | **Works**, measured 2026-10-13 (#630) |
| [`sbx-agent/`](sbx-agent/) | Claude Code in a local sbx sandbox, a worktree mounted three ways (#613) | **Measured** 2026-10-11, feeds #614 |

The Terraform folders read an existing resource group and never manage it: `terraform destroy`
removes what the folder created and leaves the group, and anything else in it, alone.

## What was measured

The same `tc-probe` test, everywhere:

| Where | Own Docker daemon | Kernel | Testcontainers |
| --- | --- | --- | --- |
| Mac, Docker Desktop (baseline) | — | — | passes |
| Mac, `sbx` v0.38.0 | yes, 29.8.1, invisible to Docker Desktop | microVM | passes |
| Azure D4s_v5, `sbx` v0.46.0 | yes, 29.8.1, invisible to the VM's Docker | sandbox 7.0.14, VM 6.17.0-azure | passes, 22 s with restore and build |
| No local Docker, `DOCKER_HOST` over TCP, port range open | remote | — | passes |
| No local Docker, `DOCKER_HOST` over TCP, only 2375 open | remote | — | fails: Ryuk times out at 60 s; without Ryuk, Npgsql cannot reach the mapped port |

And this repository's own gate, `aspire do ci`, with `docker` and `podman` off the `PATH`: 18 of 18
steps green. Its AppHost declares no container resources, so DCP runs everything as processes.

## sbx-vm

A D4s_v5 (generation 2, x86, nested virtualization) with Ubuntu 24.04. cloud-init installs Docker
Engine and `sbx` with Docker's own installer and adds the user to `kvm`. SSH is open to one address.

```bash
cd infra/sbx-vm
cp terraform.tfvars.example terraform.tfvars   # set ssh_source_cidr to your IP
terraform init && terraform apply              # about 1 minute
ssh -t azureuser@$(terraform output -raw public_ip) sbx login
tests/run.sh                                   # results in tests/out/results.txt
terraform destroy
```

About 0.22 € an hour while it exists: the VM, a 128 GB Premium disk and a public IP.

Three things the run taught:

- **`sbx` on Linux will not create a sandbox, not even a local one, until `sbx login`.** The
  device-code flow works over `ssh -t`. With no keychain on the VM, the session is stored on disk,
  protected only by file permissions, and it goes when the VM goes.
- **The `balanced` network policy blocks .NET.** `api.nuget.org` and `builds.dotnet.microsoft.com`
  answer 403. `tests/run.sh` adds per-sandbox allow rules; nothing global is widened.
- **The `shell` image has Docker but no .NET SDK.** The script installs it inside the sandbox.

## aks-kata

AKS on the Free tier: a `system` pool (D2s_v5, add-ons only) and a `kata` pool (D4s_v5, Azure Linux,
`workload_runtime = "KataVmIsolation"`), which gives pods the `kata-vm-isolation` RuntimeClass.
`tests/run.sh` compares the kernel a runc pod and a Kata pod report on the same node, runs
`docker:dind` privileged inside a Kata pod VM, and runs `tc-probe` against that daemon.

It cannot be created in Labsplain today, for two reasons. It failed the same way in `proposals-dev-rg-01` and in `netcoreconf-dev-rg-01`, the second with Owner on the group and a tag that names this very test, so a role on the resource group does not change either of them:

1. **The management group's "Allowed nomenclature" initiative denies it.** It requires
   `<project>-<pro|pre|qas|dev|lab>-<abbr>-<NN>` for VMs, VMSS, NICs, NSGs, public IPs, VNets,
   route tables and user-assigned identities. AKS names the resources in its `MC_…` group itself.
   The public IP, the VNet and the identity could be brought in under compliant names; the VMSS and
   the NSG cannot. The first `apply` failed on the public IP after 25 s.
2. **Pod Sandboxing needs `Microsoft.Network/AllowBringYourOwnPublicIpAddress`** registered on the
   subscription, which takes rights at subscription scope.

So it needs a policy exemption on the `MC_…` group and the feature flag, both from whoever runs the
tenant. With both in place, the folder applies unchanged:

```bash
cd infra/aks-kata
cp terraform.tfvars.example terraform.tfvars
terraform init && terraform apply
$(terraform output -raw get_credentials)
tests/run.sh
terraform destroy
```

A failed `apply` can leave a cluster in Azure that is not in the Terraform state. Remove it with
`az aks delete -g <group> -n aks-kata-probe --yes`.

### agent-sandbox on the Kata pool

`aks-kata/tests/agent-sandbox.sh` installs [kubernetes-sigs/agent-sandbox](https://github.com/kubernetes-sigs/agent-sandbox)
v1.0.2 and measures what the loop would use it for. Its manifests are checked against the v1.0.2
CRD schemas; they have not run on AKS, because the cluster cannot be created yet.

| Test | agent-sandbox object | What it stands in for |
| --- | --- | --- |
| A. cold start, 4Gi Azure Disk at `/workspace` | `Sandbox` | the worktree of one issue |
| B. `operatingMode: Suspended`, then `Running` | `Sandbox` | a hold: the pod goes, the disk stays |
| C. pool of 2, then a claim | `SandboxWarmPool`, `SandboxClaim` | the WIP limit, and a Run asking for a place |
| D. `kubectl exec -it` through a PTY | the pod behind the `Sandbox` | the console watching the Run |

Suspend deletes the pod and keeps the volume. Files survive a hold; the agent's process does not,
and comes back with the vendor's resume. The upstream example also warns that `kubectl
port-forward` does not reach a Kata pod — the listener is inside the guest VM — which is why D
uses `exec`.

## pty-demo — the talk

The console of the harness streams a PTY to the browser (ADR-0001). The demo is that idea with
nothing around it: one xterm.js page, a Node server with `node-pty`, and six backends that differ
only in the command the PTY spawns ([`backends.js`](pty-demo/backends.js)).

| Backend | Command in the PTY | TTY | First byte | Kernel | Own Docker |
| --- | --- | --- | --- | --- | --- |
| 1 · host | `zsh` | `/dev/ttys015` | 25–38 ms | 27.0.0 (macOS) | the host's |
| 2 · sbx on the laptop | `sbx exec -it pty-demo sh` | `/dev/pts/0` | 0.4–1.8 s | 7.0.12 | yes |
| 3 · sbx on the Azure VM | `ssh -tt vm sbx exec -it pty-demo sh` | `/dev/pts/0` | 0.9–3.8 s, 6.8 s cold | 7.0.14 | yes |
| 4 · agent-sandbox in Kata, on the laptop | `kubectl exec -it <pod> sh` | not a tty, 24 81 | 0.55–1.2 s | 6.18.35, its own microVM | no |
| 4b · agent-sandbox on kind | `kubectl exec -it <pod> sh` | `/dev/pts/1` | 0.3 s | 6.12.76-linuxkit, shared | no |
| 5 · ACA Sandboxes | `aca sandbox shell -l name=pty-demo -c <probe script>` | `/dev/pts/0` | 1.7–3.4 s | 6.12.8 | no |

Backend 5 runs the same PTY against a managed sandbox: `aca sandbox shell`. The sandbox group
(`ptydemo`, northeurope; the naming policy covers no `Microsoft.App` type) lives in
`netcoreconf-dev-rg-01`. It answers only to an identity holding **Container Apps SandboxGroup Data
Owner** on the group: with Contributor on a resource group the group is created and every
data-plane call answers *Access denied*. Owner on the resource group, which here came through PIM as
an *eligible* assignment that has to be activated first, lets `aca sandboxgroup create` grant the
role to the caller itself; it took about 45 s to propagate. The page shows the button disabled,
with that reason, until a list call succeeds.

What it measured, once: the sandbox is created in 1.6 s and is 1 vCPU and 2 GB, with root, git and
internet, kernel 6.12.8, no Docker. The first shell took 1.7 and then 3.4 s. It **stopped by itself**
minutes after the last connection, and a stopped sandbox does not open a shell, so the backend runs
`aca sandbox resume` before every connect (a no-op on a running one) and `demo.sh up` leaves it
running. Stopped, it bills no CPU or memory; snapshot storage is billed per GB, and I could not
confirm whether that charge is on yet. `aca sandbox shell -c` takes an executable, not a shell
line, so the probe is written into the sandbox as a script and that script is what the PTY runs.

ACA dynamic sessions need a data-plane role of their own (*Azure ContainerApps Session Executor*)
and execute code over HTTP rather than offering a terminal, so they are a slide, not a backend.

Backend 4 is agent-sandbox in Kata on the laptop, from [`kata-local`](#kata-local--kata-on-the-laptop):
its kernel is the pod VM's own. Backend 4b is the same Sandbox under runc on kind, whose kernel is
the one every container on the laptop shares; it stays for a laptop without nested virtualization.
[`pty-demo/sandbox-kata.yaml`](pty-demo/sandbox-kata.yaml) is [`pty-demo/sandbox.yaml`](pty-demo/sandbox.yaml)
plus `runtimeClassName: kata-qemu`, and that is the whole difference; on AKS it would be
`kata-vm-isolation`.

```bash
cd infra/pty-demo
./demo.sh up          # about 18 s when everything exists; stops once for `sbx login` on a new VM
./demo.sh up --no-kata --no-vm   # without the Kata VM and the Azure VM
./demo.sh serve       # http://127.0.0.1:7681
./demo.sh down        # local sandbox, Kata VM, kind cluster and the Azure VM
```

`up` rewrites the VM's SSH rule with the current public IP, because the venue's is not the
rehearsal's, and warms the VM's sandbox, because its first `exec` took 6.8 s. `serve` reads the
VM's address from `sbx-vm`'s Terraform state. `kind` has no Homebrew bottle for macOS 27; the
binary from the kubernetes-sigs release, checked against its `.sha256sum`, works.

[`pty-demo/screenshots/`](pty-demo/screenshots/) holds one capture per backend, for when the
venue's network does not cooperate.

## kata-local — Kata on the laptop

AKS with Kata cannot be created in Labsplain, and kind on macOS has no KVM, so backend 4 of the
demo shared one kernel with every container on the laptop. `kata-local` closes that gap on this Mac:
k3s in a Lima VM with nested virtualization, and Kata Containers in it, so each pod is a microVM
with its own kernel, managed as a Kubernetes pod. Measured on 13 October 2026 on the M4 Pro (24 GB,
macOS 27.0.1), over four runs of `measure.sh` in two VMs.

```bash
cd infra/kata-local
./kata.sh up            # VM, k3s, Kata and agent-sandbox; 74–90 s from nothing, image cached
eval "$(./kata.sh env)" # KUBECONFIG for this cluster, at 127.0.0.1:16443
./measure.sh            # about 12 minutes; results in out/measure.txt
./kata.sh down          # deletes the VM and its disks
```

**Why Lima.** The Podman machine that replaced Docker Desktop here is `applehv` and has no
`/dev/kvm` and no virtualization flags inside, so neither Kata nor a kind or k3d cluster on it can
boot a VM. Lima with `vmType: vz` and `nestedVirtualization: true` gives its guest EL2 and
`/dev/kvm`. `brew install lima` fails on macOS 27 ("no bottle available"), as `podman` and `kind`
did; the `lima-2.2.1-Darwin-arm64.tar.gz` release, checked against the release's `SHA256SUMS`, is
unpacked into `~/.local/opt/lima-2.2.1` and linked from `~/.local/bin`.

**What `up` builds.** [`lima.yaml`](kata-local/lima.yaml): Ubuntu 26.04, 4 CPUs, 8 GiB, 40 GiB, no
host mounts, k3s v1.36.5 without Traefik and metrics-server, helm in the guest so the Mac needs
none. Only the API server reaches the Mac, on `127.0.0.1:16443` so it never meets a kind cluster on
6443. Kata Containers 4.2.0 with `kata-deploy`'s Helm chart (`k8sDistribution=k3s`, only the QEMU
shims), then agent-sandbox v1.0.2. The kubeconfig is written into the VM's own directory, so `down`
takes it with the VM; after `down`, `~/.lima` holds only Lima's `_config`. Lima's image cache
(`~/Library/Caches/lima`, 3.4 GB) stays; `limactl prune` removes it.

### Which RuntimeClass works on arm64 nested

| RuntimeClass | Result |
| --- | --- |
| `kata-clh` | not installable: kata-deploy 4.2.0 ships Cloud Hypervisor for amd64 only, and the class exists with no handler behind it ("no runtime for kata-clh is configured") |
| `kata-fc` | the same: Firecracker is amd64 only in kata-deploy |
| `kata-qemu` | **works**, with the four settings below. The demo and the measurements use it |
| `kata-qemu-runtime-rs` | works with the same settings and a longer wait for the agent; kata-deploy's default shim on arm64 |

Out of the box, `kata-qemu` does not start a single pod here. Each setting below was found by
booting the pod VM's kernel by hand with the QEMU that kata-deploy installed, and each is a drop-in in
the runtime's `config.d`, so the shipped configuration is not edited:
[`kata-nested.toml`](kata-local/kata-nested.toml) for both QEMU runtimes,
[`kata-nested-rs.toml`](kata-local/kata-nested-rs.toml) for runtime-rs.

1. **`cpu_features = ""`.** The shipped `pmu=off` makes QEMU exit at once: the nested vCPU has no
   PMU, so the property does not exist.
2. **`firmware = ""`.** With the AAVMF UEFI firmware, QEMU ran at 100 % CPU for 40 s and the kernel
   never printed a line. Without it, the kernel is at `init` in 0.5 s.
3. **`pci=nomsi`** on the guest kernel's command line. MSI through the emulated GIC ITS never
   arrives in the pod VM: with `console=hvc0` a `sleep 5` as `init` did not return in 25 s, and the
   agent's vsock timed out. With `pci=nomsi`, or QEMU's `its=off`, it returns in 5 s.
4. **`reconnect_timeout_ms = 60000`**, runtime-rs only. It gives the agent 3 s by default and the
   pod VM boots in 11 to 28 s here, so `kata-qemu-runtime-rs` gave up where `kata-qemu`, which waits
   45 s, did not. The Go runtime refuses a drop-in with a key it does not know, so this one has a
   file of its own.

### What was measured

Two pods on the one node, `manifests/kernel.yaml`:

```
probe-runc (runc, node lima-kata-local): 7.0.0-34-generic
probe-kata (kata-qemu, node lima-kata-local): 6.18.35
probe-kata sees 1 CPU, 1989 MiB
```

The runc pod sees the VM's Ubuntu kernel; the Kata pod sees the kernel kata-deploy ships, in a
microVM of 1 vCPU and 2 GB (`default_memory`). Inside the Lima VM, `dmesg` says `kvm [1]: Hyp nVHE
mode initialized successfully` and `/dev/kvm` is `crw-rw---- root kvm 10, 232`.

| Measure | Result |
| --- | --- |
| `kata.sh up` from nothing | 74, 82 and 90 s, the Ubuntu image already in Lima's cache |
| Cold start, `kubectl run` to Ready | runc 0.48–0.57 s with the image on the node, 2.8 s with the pull; `kata-qemu` 11.2–18.7 s in a fresh VM, up to 30.0 s after an hour of runs under the Mac's memory pressure (below). Of one 31 s start, 27.7 s went from QEMU's launch to the agent answering |
| Memory of one Kata pod, in the VM | QEMU 194–207 MiB resident, of it 157–169 MiB guest memory in `/dev/shm`; `containerd-shim-kata-v2` 31–33 MiB; two `virtiofsd` 5 MiB. About 230 MiB a pod |
| Memory per pod, from the node | `MemAvailable` falls 210–220 MiB per pod over 4, 8 and 12 pods |
| Pods at once, 4 CPUs and 8 GiB | 15 scheduled, then *Insufficient cpu*: the `kata-qemu` RuntimeClass charges `overhead.podFixed` of 250m CPU and 320Mi to every pod, and 4000m less 125m for CoreDNS and kata-deploy is 15 of them. 13 to 15 were Ready within 240 s. At 15 the VM still had 3.6 GB available and `/dev/shm` (3.9 GB) was 2.4 GB used: memory alone would take about 24 |
| PTY, `kubectl exec -it` spawn to first byte | runc 52–94 ms, once 259 ms; `kata-qemu` 237–527 ms; once 7.4 s, the first exec into a pod that had just started |
| PTY inside the Kata pod | a terminal (`test -t 0` is true, `stty size` answers 30 100), but `tty` says *not a tty*: the agent allocates the PTY in the guest's devpts, so `/dev/pts` in the container is empty |
| agent-sandbox `Sandbox` in `kata-qemu`, 1Gi local-path volume | Ready in 26.8–45.2 s, volume provisioning included |
| `operatingMode: Suspended` | the pod is gone in 3.4–4.5 s; the PVC stays `Bound`; conditions `Suspended=True (PodTerminated)`, `Ready=False (SandboxSuspended)` |
| Back to `Running` | Ready again in 28.0–35.3 s; the file written before the suspend is there |

**The Mac runs short with both VMs up.** The Podman machine (8 GiB, 1.7 GB resident) and this VM
(8 GiB, all of it resident after the first round of pods) ran side by side with nothing stopped. By
the end, macOS reported 23 GB used, 9.4–11 GB in the compressor and under 200 MB free, and cold
starts grew from 11–14 s to 17–30 s over three runs of `measure.sh`. With the VM deleted, the Mac
went back to 5.4 GB free. The numbers above are with Podman up; the talk's laptop should close what
else it can, or give the VM 6 GiB.

### Backend 4 of the demo

`pty-demo` gains *4 · agent-sandbox en Kata (portátil)*: the same `kubectl exec -it` into the pod
behind the `pty-demo` Sandbox, with `KUBECONFIG` pointing at this cluster and
[`sandbox-kata.yaml`](pty-demo/sandbox-kata.yaml), which is `sandbox.yaml` plus
`runtimeClassName: kata-qemu`. kind stays as *4b*, for a laptop without nested virtualization. The
page showed kernel 6.18.35 and a first byte of 551 ms to 1.2 s; the first byte there includes the
two `kubectl get` calls that find the pod.
[`4-agent-sandbox-kata.jpg`](pty-demo/screenshots/4-agent-sandbox-kata.jpg) is the capture.

### What a KataEnvironment would be

A recommendation for a later issue, not built here. `KataEnvironment` would sit behind
`IEnvironment` (#612) beside `worktree` and `sbx` (#614), and take from `SbxEnvironment` what the
#613 probe already settled.

- **The code: a clone in a volume, as sbx's `--clone`.** `PrepareAsync` creates a `Sandbox` whose
  `volumeClaimTemplates` give `/workspace`, and an init container clones the folder's repository
  into it at the PBI's branch. Mounting the host's worktree does not carry over: this VM has no host
  mounts, and on AKS there is no host. The clone also keeps the host's hooks and `.git/config` out
  of reach, which is why sbx uses one.
- **The branch comes back through the remote.** The run pushes its branch as it does today, and
  `RetireAsync` fetches it into the worktree, where the gate runs. Without a credential inside, the
  alternative is a `git bundle` copied out with `kubectl cp` before the pod goes: it needs nothing
  inside, and it is one more step that can fail.
- **Credentials enter without entering.** Two ways, neither measured here, in order of preference: a
  proxy on the host that the pod reaches, which holds the secret and adds it on the way out, as sbx
  does; or a Kubernetes `Secret` per Sandbox, created by `PrepareAsync`, mounted only into that pod
  and deleted by `RetireAsync`, with a fine-grained GitHub token for one repository. The first keeps
  the token out of the pod VM; the second is simpler and is what AKS would offer.
- **The session wraps `kubectl exec -it`.** `Enter` returns `kubectl exec -it <pod> -- <executable>
  <args>` with the directory and the variables passed through `env`, and the PTY and its recording
  stay on this machine, as with sbx. The end record does not: a run directory cannot be mounted
  from the host, so `AIO_DONE_FILE` points inside the pod and is read with `kubectl exec cat` when
  the session ends. The 0.24–0.53 s first byte is fine for a console; the
  *not a tty* from `tty` is cosmetic, but a CLI that checks `ttyname` would notice.
- **A hold suspends the Sandbox.** `operatingMode: Suspended` deletes the pod VM and keeps the
  volume, so the clone and anything uncommitted survive and the agent's process does not; resume
  costs a cold start, 28–35 s here. A hold that must keep the process alive cannot use it.
- **Readiness.** Offered when the cluster answers, the RuntimeClass exists and agent-sandbox's CRDs
  are installed; the remedy is `infra/kata-local/kata.sh up` on a laptop, and a kubeconfig on AKS.
- **Cost.** A pod VM takes 11–30 s to start and about 230 MiB, and this VM schedules 15. With `wip:
  4` that fits on the laptop with room to spare; the start is the price, and a `SandboxWarmPool`
  pays it before a run asks.

## sbx-agent — Claude Code inside a sandbox

`pty-demo` opens a shell in sbx. This probe opens the agent: `sbx create … claude`, the template
#614's adapter would use, with a worktree of a throwaway repository mounted three ways. It answers
how Claude Code authenticates in there, whether git and `aio done` work, which hosts it reaches, and
what an agent in `yolo` can still touch. Measured on the M4 Pro with sbx v0.38.0, the global policy
`balanced`, and `aio-dogfood` as the repository.

```bash
cd infra/sbx-agent
REPO=/path/to/throwaway/repo ./probe.sh          # about 2 minutes; results in out/results.txt
REPO=/path/to/throwaway/repo ./probe.sh hello    # only claude -p "di hola"
./probe.sh down                                  # removes any probe-613-* sandbox left behind
```

It creates one sandbox at a time and removes each before the next: every sandbox took 2.4 to 5.4 GB
of disk, and three at once filled the 13 GB this laptop had free. It pushes one branch per mount to
the repository's origin and deletes it again from the host. Network rules and secrets it adds are
scoped to its own sandboxes; the global policy and the global secrets are not touched.
[`inside.sh`](sbx-agent/inside.sh) is what runs in the sandbox, [`network.sh`](sbx-agent/network.sh)
asks one host at a time.

### What was measured

| Measure | Result |
| --- | --- |
| `sbx create … claude`, image cached | 4.0–5.4 s over 14 timed creates; 10.3 s the first time, with the pull |
| First `exec` after create | 0.23–0.31 s: `create` leaves the sandbox running |
| `exec` on a running sandbox | 0.23–0.29 s |
| `exec` on a stopped sandbox | 1.0–1.2 s, once 4.1 s; an idle sandbox stops by itself |
| Workspace path inside | the host's path, for every bind mount and for `--clone` |
| Permission mode | the template writes `defaultMode: bypassPermissions` into `~/.claude/settings.json` |
| Claude Code | 2.1.280; no .NET SDK in the image (`node`, `npm`, `git`, `gh`, `docker` are there) |
| Claude credential by default | none: `SBX_CRED_ANTHROPIC_MODE=none`, `claude -p` answers "Not logged in" in 0.7–1.0 s |
| `aio done` with `AIO_DONE_FILE` in a mounted run directory | written, and readable on the host at the same path at once, in all three mounts |
| `aio done` without `AIO_DONE_FILE` | lands in the worktree's `.aio/done.json` for the bind mounts; in `--clone`, in the private clone, invisible to the host |
| Hosts reached under `balanced` | `api.anthropic.com`, `github.com`, `api.github.com`, `codeload.github.com`, `objects.githubusercontent.com`, `registry.npmjs.org` |
| Hosts `balanced` blocks that .NET needs | `api.nuget.org`, `www.nuget.org`, `builds.dotnet.microsoft.com`: 403 until a per-sandbox allow, then they answer |
| Hosts `balanced` lets out that a run does not need | `gist.github.com`, `raw.githubusercontent.com`, `s3.amazonaws.com`, `*.blob.core.windows.net`, and the rest of its 192 rules (OpenAI, Google, PyPI, …) |
| Hosts `balanced` blocks | `example.com`, `pastebin.com`, `webhook.site`: 403 from the proxy |

### claude -p "di hola"

Not answered yet. In every sandbox the probe made, the agent had no credential:

```
$ sbx exec -w <worktree> probe-613-hello claude -p "di hola"
Not logged in · Please run /login
```

sbx never puts a credential in the sandbox. Its proxy holds the secret on the host and injects it on
the way out, and the template points Claude Code at it. This is what the probe saw:

- **An `anthropic` secret present when the sandbox is created** turns the sandbox's mode to `apikey`,
  writes an `apiKeyHelper` into `~/.claude/settings.json`, and `claude auth status` says
  `loggedIn: true, authMethod: api_key_helper`. The key itself stays on the host. Measured with a
  deliberately invalid key scoped to one sandbox.
- **The same secret stored after the sandbox exists changes nothing**: the mode stays `none`. It is
  read at create.
- **A subscription login** goes through `sbx run claude … -- auth login`, which sbx documents as
  storing an OAuth token on the host for every sandbox. It needs the person's browser, so the probe
  did not do it.

It answers once a credential exists. The narrowest way, scoped to one sandbox and removed again on
exit, is `sbx secret set anthropic --sandbox probe-613-hello` (sbx prompts for the value, so it
never reaches a shell history or a log), then `./probe.sh hello`.

### GitHub from inside

`GH_TOKEN` inside is a placeholder, `gho_sbxproxy…`. On `github.com` and `api.github.com` the proxy
intercepts TLS and swaps in the `github` secret stored on the host. There is no git credential
helper and the forwarded SSH agent has no identities, so that swap is the only credential.

- **A push from inside works**: `git push` of a probe branch to `origin` created it on GitHub, from
  both the `.git` mount and the clone.
- **The secret it uses is the host's global one**, and `gh api -i user` shows its scopes: `copilot,
  gist, project, read:org, repo, workflow`. An agent in `yolo` can write to every repository the
  person can, edit workflows, and post a gist through `gist.github.com`, which `balanced` allows.
- **A secret scoped to the sandbox wins at once.** With an invalid one set by
  `sbx secret set github --sandbox <name>`, `gh api user` answered `Bad credentials` on the next
  call, without a restart.
- **A sandbox's secrets outlive the sandbox.** After `sbx rm` both probe secrets were still listed;
  `sbx secret rm <service> --sandbox <name>` removes them.

### What an agent in yolo can touch, by mount

| | Worktree only | Worktree and the repository's `.git` | `--clone` of the repository |
| --- | --- | --- | --- |
| Created with | `claude <wt> <run-dir>` | `claude <wt> <run-dir> <repo>/.git` | `--clone claude <repo> <run-dir>` |
| git | **does not work**: the worktree's `.git` file points at `<repo>/.git/worktrees/<name>`, which is not there | works; the commit lands on the host's branch at once | works, on a private clone at the host's path |
| Files | the worktree and the run directory, read-write | those, and the repository's whole `.git`, read-write | the clone and the run directory; the host's repository is not writable |
| Branches | none | all 5 refs, and it can move any of them; the other worktree's metadata | all 5, copied; the host's refs do not move. Its commits reach the host as `sandbox-<name>/*`, fetched over sbx's git-daemon |
| Hooks and `.git/config` | — | **writable**: a hook or `core.hooksPath` written here runs on the host the next time the host runs git, which the engine does all the time | the clone's only |
| Network | `balanced` plus the kit's 3 hosts | same | same |
| Credentials | the host's GitHub secret through the proxy; no Anthropic secret unless one is stored; no SSH identities; `~/.ssh` and `~/.claude` of the host are not visible | same | same |

`--clone` refuses a worktree as its path ("not supported when run from a Git worktree"): it takes the
main repository, and the clone starts on that checkout's branch.

### The network rules

A per-sandbox rule can only add to the global policy and to the kit, never replace it. `allow`
widens: the three .NET hosts went from 403 to an answer. `deny` narrows: `gist.github.com`,
`raw.githubusercontent.com`, `s3.amazonaws.com` and `*.blob.core.windows.net` went to 403. But deny
always wins, so a per-sandbox `deny **` followed by `allow api.anthropic.com` blocks Anthropic too:
**a minimal allow list cannot be written per sandbox on top of `balanced`**. It takes `sbx policy
init deny-all` globally, which changes every sandbox on the machine, so the probe did not do it.

The list a run needs, from what the proxy logged: `api.anthropic.com`, `github.com`,
`api.github.com`, `codeload.github.com`, `objects.githubusercontent.com`, `registry.npmjs.org`, and
for .NET `api.nuget.org`, `*.nuget.org`, `builds.dotnet.microsoft.com`. The kit adds `claude.com`,
`downloads.claude.ai` and `mcp-proxy.anthropic.com` by itself.

### What #614 can take from it

- **Mount: `--clone`.** The worktree alone has no git. The repository's `.git` makes git work but
  hands the agent the host's hooks and config, which is a way out of the sandbox. The clone keeps the
  host's repository read-only, and its commits come back as a remote the host fetches. Because it
  takes the main repository and not the worktree, the adapter switches the clone to the PBI's branch
  first and, after the run, fetches `sandbox-<name>/<branch>` into the worktree, where the gate runs
  as today.
- **The end record: the run's directory as a second workspace**, with `AIO_DONE_FILE` set to the
  same path through `sbx exec -e`. It is written inside and read outside with nothing copied. The
  recording is the host's PTY around `sbx exec -it`, as in `pty-demo`; nothing about it changes.
- **Claude: an `anthropic` secret stored before `sbx create`**, scoped to the sandbox or global. The
  adapter checks `SBX_CRED_ANTHROPIC_MODE` is not `none` before it launches, and says how to fix it
  when it is.
- **GitHub: a fine-grained token per repository**, stored with `sbx secret set github --sandbox
  <name>` so it replaces the global one inside: that repository only, Contents and Pull requests
  read-write, Issues read-write for the refine and review commands. The commands keep pushing
  themselves. Pushing from the host after the run, with no token inside, also works with the clone,
  but `/aio:propose`, `/aio:implement` and the `gh` reads in every command would have to change.
  The adapter removes the sandbox's secrets with the sandbox.
- **Network: `balanced` with per-sandbox rules**: allow the three .NET hosts, deny `gist.github.com`,
  `raw.githubusercontent.com`, `*.amazonaws.com` and `*.blob.core.windows.net`. A true allow list
  is a global `deny-all`, the person's decision.
- **Disk and image.** One sandbox per PBI at 2.4–5.4 GB each is about 20 GB at `wip: 4`. The
  `claude` image has no .NET SDK, so an agent that builds needs a template with it (`-t`) or an
  install at start; the gate stays on the host.
