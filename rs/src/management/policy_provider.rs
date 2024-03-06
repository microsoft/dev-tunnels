use std::io;

#[cfg(target_os = "windows")]
pub fn get_policy_header_value() -> io::Result<Option<String>> {
    use urlencoding::encode;
    use winreg::enums::*;
    use winreg::RegKey;

    pub const REGISTRY_KEY_PATH: &str = r"Software\Policies\Microsoft\DevTunnels";

    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    let sub_key = match hklm.open_subkey(REGISTRY_KEY_PATH) {
        Ok(sub_key) => sub_key,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(e) => return Err(e),
    };

    let mut header_values = vec![];

    for (name, value) in sub_key.enum_values().filter_map(Result::ok) {
        let value_str: String = value.to_string();
        if !value_str.is_empty() {
            header_values.push(format!("{}={}", encode(&name), encode(&value_str)));
        }
    }

    let header = header_values.join("; ");
    if header.is_empty() {
        Ok(None)
    } else {
        Ok(Some(header))
    }
}

#[cfg(not(target_os = "windows"))]
pub fn get_policy_header_value() -> io::Result<Option<String>> {
    Ok(None)
}
