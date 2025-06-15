using System.Buffers;
using System.Collections;

namespace WebHost.MemoryBuffers;

/// <summary>
/// Represents a high-performance, pooled dictionary that minimizes allocations by renting internal arrays from <see cref="ArrayPool{T}"/>.
/// This structure is optimized for small, short-lived dictionaries such as HTTP headers or per-request state.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary. Must implement <see cref="IEquatable{T}"/>.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public class PooledDictionary<TKey, TValue> :
    IDictionary<TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>,
    IDisposable
    where TKey : IEquatable<TKey>
{
    private const int DefaultCapacity = 8;
    private const int MaxCapacity = 1024;

    private static readonly ArrayPool<TKey> KeyPool = ArrayPool<TKey>.Shared;
    private static readonly ArrayPool<TValue> ValuePool = ArrayPool<TValue>.Shared;

    private TKey[]? _keys;
    private TValue[]? _values;
    private int _count;
    private bool _disposed;

    private readonly IEqualityComparer<TKey> _comparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="capacity">Initial capacity of the dictionary.</param>
    /// <param name="comparer">An optional custom equality comparer for keys.</param>
    public PooledDictionary(int capacity = DefaultCapacity, IEqualityComparer<TKey>? comparer = null)
    {
        _keys = KeyPool.Rent(capacity);
        _values = ValuePool.Rent(capacity);
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
    }

    /// <summary>
    /// Gets the number of key-value pairs contained in the dictionary.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the dictionary is read-only.
    /// Always returns false.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets a collection containing the keys in the dictionary.
    /// </summary>
    public ICollection<TKey> Keys => GetKeys();

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => GetKeys();

    /// <summary>
    /// Gets a collection containing the values in the dictionary.
    /// </summary>
    public ICollection<TValue> Values => GetValues();

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => GetValues();

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            EnsureNotDisposed();
            for (int i = 0; i < _count; i++)
            {
                if (_comparer.Equals(_keys![i], key))
                {
                    return _values![i];
                }
            }

            throw new KeyNotFoundException();
        }
        set
        {
            EnsureNotDisposed();

            for (int i = 0; i < _count; i++)
            {
                if (_comparer.Equals(_keys![i], key))
                {
                    _values![i] = value;
                    return;
                }
            }

            Add(key, value);
        }
    }

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        EnsureNotDisposed();

        if (ContainsKey(key))
            throw new ArgumentException("An element with the same key already exists.");

        EnsureCapacity();
        _keys![_count] = key;
        _values![_count] = value;
        _count++;
    }

    /// <summary>
    /// Adds the specified key-value pair to the dictionary.
    /// </summary>
    public void Add(KeyValuePair<TKey, TValue> item)
        => Add(item.Key, item.Value);

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            if (_comparer.Equals(_keys![i], key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific key-value pair.
    /// </summary>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            if (_comparer.Equals(_keys![i], item.Key) &&
                EqualityComparer<TValue>.Default.Equals(_values![i], item.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            if (_comparer.Equals(_keys![i], key))
            {
                value = _values![i];
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Removes all keys and values from the dictionary.
    /// </summary>
    public void Clear()
    {
        EnsureNotDisposed();
        _count = 0;
    }

    /// <summary>
    /// Copies the elements of the dictionary to an array, starting at a particular index.
    /// </summary>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        EnsureNotDisposed();

        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex + _count > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        for (int i = 0; i < _count; i++)
        {
            array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(_keys![i], _values![i]);
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    public bool Remove(TKey key)
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            if (_comparer.Equals(_keys![i], key))
            {
                RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the specified key-value pair from the dictionary.
    /// </summary>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            if (_comparer.Equals(_keys![i], item.Key) &&
                EqualityComparer<TValue>.Default.Equals(_values![i], item.Value))
            {
                RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            yield return new KeyValuePair<TKey, TValue>(_keys![i], _values![i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Releases the rented arrays and clears the dictionary.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_keys != null)
        {
            KeyPool.Return(_keys, clearArray: true);
            _keys = null!;
        }

        if (_values != null)
        {
            ValuePool.Return(_values, clearArray: true);
            _values = null!;
        }

        _count = 0;
    }

    private void RemoveAt(int index)
    {
        if (index < _count - 1)
        {
            Array.Copy(_keys!, index + 1, _keys!, index, _count - index - 1);
            Array.Copy(_values!, index + 1, _values!, index, _count - index - 1);
        }

        _count--;
        _keys![_count] = default!;
        _values![_count] = default!;
    }

    private void EnsureCapacity()
    {
        if (_count < _keys!.Length)
            return;

        int newSize = Math.Min(_keys.Length * 2, MaxCapacity);

        var newKeys = KeyPool.Rent(newSize);
        var newValues = ValuePool.Rent(newSize);

        Array.Copy(_keys, newKeys, _count);
        Array.Copy(_values, newValues, _count);

        KeyPool.Return(_keys, clearArray: true);
        ValuePool.Return(_values, clearArray: true);

        _keys = newKeys;
        _values = newValues;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledDictionary<TKey, TValue>));
    }

    private ICollection<TKey> GetKeys()
    {
        var list = new List<TKey>(_count);
        for (int i = 0; i < _count; i++)
            list.Add(_keys![i]);

        return list;
    }

    private ICollection<TValue> GetValues()
    {
        var list = new List<TValue>(_count);
        for (int i = 0; i < _count; i++)
            list.Add(_values![i]);

        return list;
    }
}