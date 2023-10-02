import { TunnelConstraints } from "../contracts/tunnelConstraints"

export class IdGeneration {
    private static nouns: string[] = ["pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "rabbit", "shoe", "campsite", "plane", "sofa", "chair", "library", "book", "ocean", "lake", "river", "horse"];
    private static adjectives: string[] = ["fun", "happy", "interesting", "neat", "peaceful", "puzzeled", "thoughtful", "kind", "joyful", "overjoyed", "new", "giant", "sneaky", "quick", "majestic", "gleaming", "jolly", "fancy", "tidy", "marvelous", "glamorous", "swift", "silent", "amusing", "spiffy"];

    public static generateTunnelId(): string {
        let tunnelId = "";
        tunnelId += this.adjectives[Math.floor(Math.random() * this.adjectives.length)] + "-";
        tunnelId += this.nouns[Math.floor(Math.random() * this.nouns.length)] + "-";
        for (let i = 0; i < 7; i++) {
            tunnelId += TunnelConstraints.oldTunnelIdChars[Math.floor(Math.random() * (TunnelConstraints.oldTunnelIdChars.length))];
        }
        return tunnelId;
    }
}