using System.Buffers;
using System.Collections;
using System.Collections.Generic;

namespace WebHost;

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

    private static readonly ArrayPool<TKey> _keyPool = ArrayPool<TKey>.Shared;
    private static readonly ArrayPool<TValue> _valuePool = ArrayPool<TValue>.Shared;

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
        _keys = _keyPool.Rent(capacity);
        _values = _valuePool.Rent(capacity);
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ICollection<TKey> Keys => GetKeys();

    /// <inheritdoc />
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => GetKeys();

    /// <inheritdoc />
    public ICollection<TValue> Values => GetValues();

    /// <inheritdoc />
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => GetValues();

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Add(KeyValuePair<TKey, TValue> item)
        => Add(item.Key, item.Value);

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Clear()
    {
        EnsureNotDisposed();
        _count = 0;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        EnsureNotDisposed();

        for (int i = 0; i < _count; i++)
        {
            yield return new KeyValuePair<TKey, TValue>(_keys![i], _values![i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_keys != null)
        {
            _keyPool.Return(_keys, clearArray: true);
            _keys = null!;
        }

        if (_values != null)
        {
            _valuePool.Return(_values, clearArray: true);
            _values = null!;
        }

        _count = 0;
    }

    private void EnsureCapacity()
    {
        if (_count < _keys!.Length)
            return;

        int newSize = Math.Min(_keys.Length * 2, MaxCapacity);

        var newKeys = _keyPool.Rent(newSize);
        var newValues = _valuePool.Rent(newSize);

        Array.Copy(_keys, newKeys, _count);
        Array.Copy(_values, newValues, _count);

        _keyPool.Return(_keys, clearArray: true);
        _valuePool.Return(_values, clearArray: true);

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