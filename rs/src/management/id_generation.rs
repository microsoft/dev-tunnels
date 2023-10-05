extern crate rand;
use rand::Rng;

pub struct IdGeneration;

impl IdGeneration {
    const NOUNS: [&'static str; 21] = ["pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "rabbit", "shoe", "campsite", "plane", "cake", "sofa", "chair", "library", "book", "ocean", "lake", "river", "horse"];
    const ADJECTIVES: [&'static str; 24] = ["fun", "happy", "interesting", "neat", "peaceful", "puzzeled", "thoughtful", "kind", "joyful", "overjoyed", "new", "giant", "sneaky", "quick", "majestic", "gleaming", "jolly", "fancy", "tidy", "marvelous", "glamorous", "swift", "silent", "amusing", "spiffy"];
    const TUNNEL_ID_CHARS: &'static str = "bcdfghjklmnpqrstvwxz0123456789";

    pub fn generate_tunnel_id() -> String {
        let mut rng = rand::thread_rng();
        let mut tunnel_id = String::new();
        tunnel_id.push_str(Self::ADJECTIVES[rng.gen_range(0, Self::ADJECTIVES.len())]);
        tunnel_id.push('-');
        tunnel_id.push_str(Self::NOUNS[rng.gen_range(0, Self::NOUNS.len())]);
        tunnel_id.push('-');

        for _ in 0..7 {
            tunnel_id.push(Self::TUNNEL_ID_CHARS.chars().nth(rng.gen_range(0, Self::TUNNEL_ID_CHARS.len())).unwrap());
        }
        tunnel_id
    }
}