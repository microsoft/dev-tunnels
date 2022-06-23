use chrono::{DateTime, Utc};
use serde::{Serialize, Deserialize};

#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct Tunnel {
    tunnel_id: String,
    created: DateTime<Utc>,
}
