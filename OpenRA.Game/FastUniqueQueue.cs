using System.Collections.Generic;

namespace OpenRA
{
	/// <summary>
	/// Datastructure with the abilities of both an ordered list/queue and the uniqueness and O(1) access time of a dictionary.
	/// Drawback is uses slightly more memory pr item compared to a queue and more time per insertion than dictionary.
	/// </summary>
	public class FastUniqueQueue<K, V> where K : struct where V : class
	{
		readonly Dictionary<K, FastQueueEntry<K, V>> map = new Dictionary<K, FastQueueEntry<K, V>>();
		FastQueueEntry<K, V> head = null;
		FastQueueEntry<K, V> tail = null;
		readonly bool orderIsFIFO = true;

		public FastUniqueQueue() { }

		public FastUniqueQueue(bool fifoOrder)
		{
			orderIsFIFO = fifoOrder;
		}

		/// <summary>
		/// Default behavior is to AddLast().
		/// </summary>
		public bool Add(K key, V value)
		{
			if (orderIsFIFO)
				return AddLast(key, value);
			else
				return AddFirst(key, value);
		}

		/// <summary>
		/// If key is already contained returns false and entry will be moved to the head.
		/// </summary>
		/// <returns>Returns true if entry was added.</returns>
		public bool AddFirst(K key, V value)
		{
			var entry = GetEntry(key);
			var added = entry == null;

			if (added)
			{
				var newTail = new FastQueueEntry<K, V>(value, key, null, tail?.Key);
				if (tail != null)
					tail.Next = key;
				tail = newTail;

				if (head == null)
					head = newTail;

				map.TryAdd(key, newTail);
			}
			else
			{
				if (EqualityComparer<K>.Default.Equals(head.Key, entry.Key))
					return added; // If already head, we skip ALL the head updating below.

				var prevStitch = GetEntry(entry.Previous);
				var nextStitch = GetEntry(entry.Next);

				if (prevStitch != null)
					prevStitch.Next = entry.Next;
				if (nextStitch != null)
					nextStitch.Previous = entry.Previous;

				if (EqualityComparer<K>.Default.Equals(entry.Key, tail.Key))
					tail = prevStitch ?? entry;

				entry.Next = head.Key;
				entry.Previous = null;

				head.Previous = key;
				head = entry;
			}

			return added;
		}

		/// <summary>
		/// If key is already contained returns false and entry will be moved to the tail.
		/// </summary>
		/// <returns>Returns true if entry was added.</returns>
		public bool AddLast(K key, V value)
		{
			var entry = GetEntry(key);
			var added = entry == null;

			if (added)
			{
				var newTail = new FastQueueEntry<K, V>(value, key, null, tail?.Key);
				if (tail != null)
					tail.Next = key;
				tail = newTail;

				if (head == null)
					head = newTail;

				map.TryAdd(key, newTail);
			}
			else
			{
				if (EqualityComparer<K>.Default.Equals(tail.Key, entry.Key))
					return added; // If already tail, we skip ALL the tail updating below.

				var prevStitch = GetEntry(entry.Previous);
				var nextStitch = GetEntry(entry.Next);

				if (prevStitch != null)
					prevStitch.Next = entry.Next;
				if (nextStitch != null)
					nextStitch.Previous = entry.Previous;

				if (EqualityComparer<K>.Default.Equals(entry.Key, head.Key))
					head = nextStitch ?? entry;

				entry.Previous = tail.Key;
				entry.Next = null;

				tail.Next = key;
				tail = entry;
			}

			return added;
		}

		public bool PutIfAbsent(K key, V value)
		{
			return Add(key, value);
		}

		public V Peek()
		{
			return head?.Value;
		}

		public V PeekLast()
		{
			return tail?.Value;
		}

		/// <summary>
		/// Removes and gets the head of the queue.
		/// </summary>
		public V Poll()
		{
			if (head == null)
				return null;

			return Remove(head.Key);
		}

		/// <summary>
		/// Removes and gets the tail of the queue.
		/// </summary>
		public V PollLast()
		{
			if (tail == null)
				return null;

			return Remove(tail.Key);
		}

		public V Get(K key)
		{
			var entry = GetEntry(key);
			return entry?.Value;
		}

		public bool ContainsKey(K key)
		{
			return map.ContainsKey(key);
		}

		public V Remove(K key)
		{
			var entry = GetEntry(key);
			map.Remove(key);

			if (entry == null)
				return null;

			var prevStitch = entry.Previous != null ? GetEntry(entry.Previous) : null;
			var nextStitch = entry.Next != null ? GetEntry(entry.Next) : null;

			if (prevStitch != null)
				prevStitch.Next = entry.Next;
			if (nextStitch != null)
				nextStitch.Previous = entry.Previous;

			if (EqualityComparer<K>.Default.Equals(entry.Key, head.Key))
				head = nextStitch;
			if (EqualityComparer<K>.Default.Equals(entry.Key, tail.Key))
				tail = prevStitch;

			return entry.Value;
		}

		public bool BrokenStateCheck()
		{
			if (map.Count == 0)
				return head != null || tail != null;

			if (map.Count == 1)
				return tail == null || head == null || head.Next != null || head.Previous != null || tail.Next != null || tail.Previous != null || map[head.Key] == null;

			if (tail == null || head == null || tail.Next != null || head.Previous != null)
				return true;

			var entry = head;
			var nullNexts = 0;
			var nullPrevs = 0;
			var len = 0;
			while (entry != null)
			{
				len++;
				if (entry.Next == null)
					nullNexts++;
				if (entry.Previous == null)
					nullPrevs++;
				entry = GetEntry(entry.Next);
			}

			return len != map.Count || nullNexts != 1 || nullPrevs != 1;
		}

		public bool IsEmpty()
		{
			return map.Count == 0;
		}

		public int Size()
		{
			return map.Count;
		}

		public FastQueueEntry<K, V> GetEntry(K? key)
		{
			if (!key.HasValue || !map.ContainsKey(key.Value))
				return null;
			return map[key.Value];
		}

		public FastQueueEntry<K, V> HeadEntry()
		{
			return head;
		}

		public FastQueueEntry<K, V> TailEntry()
		{
			return tail;
		}
	}
}
