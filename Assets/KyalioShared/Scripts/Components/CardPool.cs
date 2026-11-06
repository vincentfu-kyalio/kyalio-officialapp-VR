using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kyalio.Components
{
    /// <summary>
    /// Reuses list items (cards, rows) instead of Instantiate / Destroy on every rebind.
    /// Get() hands out an active item appended at the end of the parent; ReturnAll()
    /// deactivates every handed-out item and keeps it for the next Get().
    ///
    /// Create it lazily from the owner's serialized prefab: an owner on an inactive
    /// GameObject has not run Awake yet when a page first binds it.
    /// </summary>
    public class CardPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Action<T> _onReturn;

        private readonly List<T> _free = new();
        private readonly List<T> _active = new();

        /// <param name="onReturn">Runs on each item as it is returned (e.g. cancel its image loads).</param>
        /// <param name="destroyExisting">
        /// Destroys <typeparamref name="T"/> children already under the parent (placeholders
        /// left in the scene), which the pool would otherwise never hide.
        /// </param>
        public CardPool(T prefab, Transform parent, Action<T> onReturn = null, bool destroyExisting = false)
        {
            _prefab = prefab;
            _parent = parent;
            _onReturn = onReturn;

            if (!destroyExisting || parent == null) return;
            foreach (Transform child in parent)
                if (child.TryGetComponent<T>(out _))
                    Object.Destroy(child.gameObject);
        }

        /// <summary>Items handed out since the last ReturnAll, in Get order.</summary>
        public IReadOnlyList<T> Active => _active;

        public T Get()
        {
            T item;
            if (_free.Count > 0)
            {
                item = _free[^1];
                _free.RemoveAt(_free.Count - 1);
            }
            else
            {
                item = Object.Instantiate(_prefab, _parent);
            }

            item.gameObject.SetActive(true);
            // Keep hierarchy order aligned with data order so the layout group renders
            // items consistently. The free list is LIFO, so without this the visible
            // order would flip each time the same list is shown again.
            item.transform.SetAsLastSibling();
            _active.Add(item);
            return item;
        }

        public void ReturnAll()
        {
            foreach (var item in _active)
            {
                if (item == null) continue;
                _onReturn?.Invoke(item);
                item.gameObject.SetActive(false);
                _free.Add(item);
            }
            _active.Clear();
        }
    }
}
