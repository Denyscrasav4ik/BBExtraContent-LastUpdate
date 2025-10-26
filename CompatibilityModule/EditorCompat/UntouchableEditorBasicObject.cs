using PlusLevelStudio.Editor;

namespace BBTimes.CompatibilityModule.EditorCompat;

public class UntouchableEditorBasicObject : EditorBasicObject, IEditorInteractable
{
    public new bool OnClicked() => false;
}