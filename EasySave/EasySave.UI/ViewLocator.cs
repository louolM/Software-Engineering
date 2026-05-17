using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using EasySave.UI.ViewModels;

namespace EasySave.UI;

// Avalonia data template that resolves a ViewModel to its corresponding View at runtime.
// When Avalonia needs to render a ViewModel it calls Build(), which derives the View type
// name by replacing "ViewModels" with "Views" and "ViewModel" with "View" in the full
// type name, then instantiates that type via reflection.
// For example, JobListViewModel -> EasySave.UI.Views.JobListView.
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views")
            .Replace("ViewModel", "View");

        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        // Fallback shown in the UI when no matching View type is found,
        // which helps diagnose naming mismatches during development.
        return new TextBlock { Text = $"View not found: {name}" };
    }

    // Only handles objects that are ViewModelBase subclasses; Avalonia
    // calls this before Build() to decide whether this template applies.
    public bool Match(object? data) => data is ViewModelBase;
}