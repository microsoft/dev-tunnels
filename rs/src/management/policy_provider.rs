use std::io;
use winreg::enums::*;
use winreg::RegKey;

pub const REGISTRY_KEY_PATH: &str = r"Software\Policies\Microsoft\Tunnels";

pub struct PolicyProvider;

impl PolicyProvider {
    pub fn new() -> Self {
        Self {
            PolicyProvider
        }
    }

    pub fn get_header_value(&self, default_on_error: &str) -> io::Result<String> {
        if cfg!(target_os = "windows") {
            let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
            let sub_key = hklm.open_subkey(REGISTRY_KEY_PATH)?;
            let mut header_values = vec![];

            for (name, value) in sub_key.enum_values().filter_map(Result::ok) {
                let value_str: String = value.into();
                if !value_str.is_empty() {
                    header_values.push(format!(
                        "{}={}",
                        urlencoding::encode(&name),
                        urlencoding::encode(&value_str)
                    ));
                }
            }

            Ok(header_values.join("; "))
        } else {
            Ok(default_on_error.to_owned())
        }
    }
}