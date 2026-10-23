output "cluster_name" {
  value = azurerm_kubernetes_cluster.probe.name
}

output "node_resource_group" {
  description = "Created and deleted by AKS with the cluster."
  value       = azurerm_kubernetes_cluster.probe.node_resource_group
}

output "get_credentials" {
  value = "az aks get-credentials -g ${data.azurerm_resource_group.target.name} -n ${azurerm_kubernetes_cluster.probe.name} --overwrite-existing"
}
