variable "subscription_id" {
  type = string
}

variable "resource_group_name" {
  description = "An existing resource group. It is read, not managed: destroy leaves it in place."
  type        = string
}

variable "project" {
  description = "First segment of every name; the tenant's policy wants it under 15 characters."
  type        = string
  default     = "sbxlab"

  validation {
    condition     = length(var.project) < 15 && !strcontains(var.project, "-")
    error_message = "Under 15 characters and no hyphen — the policy splits names on '-'."
  }
}

variable "environment" {
  type    = string
  default = "dev"

  validation {
    condition     = contains(["pro", "pre", "qas", "dev", "lab"], var.environment)
    error_message = "The tenant's policy allows pro, pre, qas, dev or lab."
  }
}

variable "vm_size" {
  description = "Generation 2 x86 with nested virtualization. D4s_v5 holds one gate-sized sandbox comfortably."
  type        = string
  default     = "Standard_D4s_v5"
}

variable "os_disk_gb" {
  description = "Sandboxes, their images and the Docker layers inside them all land here."
  type        = number
  default     = 128
}

variable "admin_username" {
  type    = string
  default = "azureuser"
}

variable "ssh_public_key_path" {
  type    = string
  default = "~/.ssh/id_rsa.pub"
}

variable "ssh_source_cidr" {
  description = "The only address allowed to SSH in."
  type        = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
