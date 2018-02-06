# CopyCtorCommand VS Extension

Initialize class copying values from similar type

## How to use
Right click inside the body of a constructor and select "Try Insert Copy Constructor".

If the caret is not positioned inside a constructor nothing will happen.

You can also set a keyboard shortcut, the full command path is "EditorContextMenus.CodeWindow.TryInsertCopyConstructor".

See [this](https://msdn.microsoft.com/en-us/library/5zwses53.aspx) link for reference on how to add a custom keyboard shortcut.


## Preview

![alt text](https://raw.githubusercontent.com/Artemoire/VSTotxtensions/master/copy-ctor-command-preview.png)

## How it works

It scans the parameters sent to the constructor and initializes containing class' public properties to the public properties of the first matching class.

A class is a match if it has atleast 1 public property of the same name.

It doesn't check for property types and missing property names in the matching class are filled with default values.

*Important!* Formatting inserted lines doesn't work, if you know how to resolve this issue please submit a new issue on my repository, thank you if you do
