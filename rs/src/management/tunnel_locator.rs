use crate::contracts::Tunnel;

#[derive(Debug)]
pub enum TunnelLocator {
    /// Tunnel by its unique name.
    Name(String),

    /// Tunnel by its ID and cluster where it's located.
    ID { cluster: String, id: String },
}

impl TryFrom<&Tunnel> for TunnelLocator {
    type Error = &'static str;

    fn try_from(tunnel: &Tunnel) -> Result<Self, Self::Error> {
        if let Some(name) = &tunnel.name {
            Ok(TunnelLocator::Name(name.to_owned()))
        } else if let (Some(cluster), Some(id)) = (&tunnel.cluster_id, &tunnel.tunnel_id) {
            Ok(TunnelLocator::ID {
                cluster: cluster.to_owned(),
                id: id.to_owned(),
            })
        } else {
            Err("Tunnel has no name or ID")
        }
    }
}
