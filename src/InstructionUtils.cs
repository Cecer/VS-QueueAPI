using System.Reflection;
using Mono.Cecil;

namespace QueueAPI;

public static class InstructionUtils
{
    public static bool Matches(this FieldReference fieldRef, FieldInfo fieldInfo)
    {
        if (fieldRef.DeclaringType.FullName != fieldInfo.DeclaringType?.FullName) return false;
        if (fieldRef.Name != fieldInfo.Name) return false;
        if (fieldRef.FieldType.FullName != fieldInfo.FieldType.FullName) return false;
        return true;
    }
}