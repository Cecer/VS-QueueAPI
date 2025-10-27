using System.Reflection;
using Mono.Cecil;

namespace QueueAPI;

public static class Extensions
{
    public static bool Matches_Fixed(this FieldReference fieldRef, FieldInfo fieldInfo)
    {
        if (fieldRef.DeclaringType.FullName != fieldInfo.DeclaringType?.FullName?.Replace("+", "/")) return false;
        if (fieldRef.Name != fieldInfo.Name) return false;
        if (fieldRef.FieldType.FullName != fieldInfo.FieldType.FullName) return false;
        return true;
    }
}