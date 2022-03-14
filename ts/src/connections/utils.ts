export class List {
    public static groupBy<T, K>(
        list: { forEach(f: (item: T) => void): void },
        keyGetter: (item: T) => K,
    ): Map<K, T[]> {
        const map = new Map();
        list.forEach((item: T) => {
            const key = keyGetter(item);
            const collection = map.get(key);
            if (!collection) {
                map.set(key, [item]);
            } else {
                collection.push(item);
            }
        });
        return map;
    }
}
