using PlusLevelStudio.Editor;

namespace BBTimes.CompatibilityModule.EditorCompat;

public class UntouchableEditorBasicObject : EditorBasicObject
{
    public new bool OnClicked() => false;
}