using System;
using System.Collections.Generic;

namespace UnityEditor
{
    public static class ArrayUtility
    {
        public static void Add<T>(ref T[] array, T item)
        {
            if (array == null)
            {
                array = new[] { item };
                return;
            }
            int size = array.Length;
            Array.Resize(ref array, size + 1);
            array[size] = item;
        }

        public static void Remove<T>(ref T[] array, T item)
        {
            if (array == null) return;
            int index = IndexOf(array, item);
            if (index < 0) return;
            RemoveAt(ref array, index);
        }

        public static void RemoveAt<T>(ref T[] array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return;
            T[] result = new T[array.Length - 1];
            if (index > 0) Array.Copy(array, 0, result, 0, index);
            if (index < array.Length - 1) Array.Copy(array, index + 1, result, index, array.Length - index - 1);
            array = result;
        }

        public static bool Contains<T>(T[] array, T item)
        {
            return IndexOf(array, item) >= 0;
        }

        public static int FindIndex<T>(T[] array, Predicate<T> match)
        {
            if (array == null || match == null) return -1;
            for (int i = 0; i < array.Length; i++)
            {
                if (match(array[i])) return i;
            }
            return -1;
        }

        public static int IndexOf<T>(T[] array, T item)
        {
            if (array == null) return -1;
            return Array.IndexOf(array, item);
        }

        public static T Find<T>(T[] array, Predicate<T> match)
        {
            if (array == null || match == null) return default!;
            int index = FindIndex(array, match);
            return index >= 0 ? array[index] : default!;
        }
    }
}
