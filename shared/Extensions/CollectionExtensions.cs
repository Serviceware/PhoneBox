using System.Collections.Generic;

namespace PhoneBox
{
    internal static class CollectionExtensions
    {
        public static ICollection<TSource> AddRange<TSource>(this ICollection<TSource> source, IEnumerable<TSource> elements)
        {
            Guard.IsNotNull(source, nameof(source));
            Guard.IsNotNull(elements, nameof(elements));

            foreach (TSource element in elements)
                source.Add(element);

            return source;
        }
    }
}