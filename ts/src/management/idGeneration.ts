import { TunnelConstraints } from "@microsoft/dev-tunnels-contracts"

export class IdGeneration {
    private static nouns: string[] = [ "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "shoe", "plane", "chair", "book", "ocean", "lake", "river" , "horse"];
    private static adjectives: string[] = ["fun", "happy", "interesting", "neat", "peaceful", "puzzled", "kind", "joyful", "new", "giant", "sneaky", "quick", "majestic", "jolly" , "fancy", "tidy", "swift", "silent", "amusing", "spiffy"];

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