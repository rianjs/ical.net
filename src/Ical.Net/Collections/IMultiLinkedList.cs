namespace Ical.Net.Collections;

public interface IMultiLinkedList<TType> :
    IList<TType>
{
    int StartIndex { get; }
    int ExclusiveEnd { get; }
}