# An AKS cluster with one Pod Sandboxing (Kata) node pool, inside a resource group that already
# exists and is not ours.
#
# The resource group is read, never managed: it holds other people's resources (the proposals
# Static Web Apps), so `terraform destroy` must remove the cluster and nothing else. The node
# resource group AKS creates beside it (MC_…) is the cluster's own and goes with it.

terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.7"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

data "azurerm_resource_group" "target" {
  name = var.resource_group_name
}

locals {
  tags = merge(var.tags, {
    purpose    = "kata-pod-sandboxing-probe"
    managed_by = "terraform"
  })
}

resource "azurerm_kubernetes_cluster" "probe" {
  name                = var.cluster_name
  location            = data.azurerm_resource_group.target.location
  resource_group_name = data.azurerm_resource_group.target.name
  dns_prefix          = var.cluster_name
  kubernetes_version  = var.kubernetes_version
  sku_tier            = "Free"

  # A throwaway probe: a local kubeconfig is simpler than Entra RBAC, and the cluster lives hours.
  local_account_disabled = false

  # The system pool runs only cluster add-ons, so every test pod lands on the Kata pool — where a
  # runc pod and a Kata pod share a node and their kernels can be compared directly.
  default_node_pool {
    name                         = "system"
    vm_size                      = var.system_vm_size
    node_count                   = 1
    os_sku                       = "AzureLinux"
    only_critical_addons_enabled = true
    temporary_name_for_rotation  = "systemtmp"
  }

  # Manual: the two pools below are the whole compute. Node auto-provisioning would pick sizes
  # itself, and the Kata pool needs one with nested virtualization.
  node_provisioning_profile {
    mode = "Manual"
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin      = "azure"
    network_plugin_mode = "overlay"
    load_balancer_sku   = "standard"
  }

  tags = local.tags
}

# Pod Sandboxing: Azure Linux only, a generation 2 x86 size with nested virtualization, and the
# RuntimeClass `kata-vm-isolation` that AKS installs with it.
resource "azurerm_kubernetes_cluster_node_pool" "kata" {
  name                  = "kata"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.probe.id
  vm_size               = var.kata_vm_size
  node_count            = var.kata_node_count
  os_sku                = "AzureLinux"
  workload_runtime      = "KataVmIsolation"

  node_labels = {
    "probe/pool" = "kata"
  }

  tags = local.tags
}
