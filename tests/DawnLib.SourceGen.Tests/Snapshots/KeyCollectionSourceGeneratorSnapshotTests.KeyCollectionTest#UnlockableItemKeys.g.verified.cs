//HintName: UnlockableItemKeys.g.cs
using Dawn;
#nullable enable
namespace My.Test.Namespace;
[System.CodeDom.Compiler.GeneratedCode("DawnLib", "<version scrubbed>")]
public static partial class UnlockableItemKeys {
	public static NamespacedKey<TestUnlockableItemInfo>? GetByReflection(string name) {
		return (NamespacedKey<TestUnlockableItemInfo>?)typeof(UnlockableItemKeys).GetField(name)?.GetValue(null);
	}
}
