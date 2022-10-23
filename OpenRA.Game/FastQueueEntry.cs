namespace OpenRA
{
	public class FastQueueEntry<K, V> where K : struct
	{
		public V Value = default;
		public K Key;
		public K? Next;
		public K? Previous;

		public FastQueueEntry(V value, K key, K? next, K? previous)
		{
			Key = key;
			Value = value;
			Next = next;
			Previous = previous;
		}

		public FastQueueEntry<K, V> Copy()
		{
			return new FastQueueEntry<K, V>(Value, Key, Next, Previous);
		}
	}
}
