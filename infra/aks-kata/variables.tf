variable "subscription_id" {
  description = "Subscription that holds the resource group."
  type        = string
}

variable "resource_group_name" {
  description = "An existing resource group. It is read, not managed: destroy leaves it in place."
  type        = string
}

variable "cluster_name" {
  description = "AKS cluster name; also the DNS prefix."
  type        = string
  default     = "aks-kata-probe"
}

variable "kubernetes_version" {
  description = "Null takes the region's default version."
  type        = string
  default     = null
}

variable "system_vm_size" {
  description = "System pool size. Runs add-ons only."
  type        = string
  default     = "Standard_D2s_v5"
}

variable "kata_vm_size" {
  description = "Kata pool size: generation 2, x86, nested virtualization. Each Kata pod is a VM, so leave room."
  type        = string
  default     = "Standard_D4s_v5"
}

variable "kata_node_count" {
  type    = number
  default = 1
}

variable "tags" {
  type    = map(string)
  default = {}
}
