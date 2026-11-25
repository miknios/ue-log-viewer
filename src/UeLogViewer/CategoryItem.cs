using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UeLogViewer;

/// One row of the category list.
public sealed class CategoryItem : INotifyPropertyChanged
{
	readonly Action<CategoryItem> onVisibilityChanged;
	bool isVisible;
	int count;

	public CategoryItem(string name, bool isVisible, Action<CategoryItem> onVisibilityChanged)
	{
		Name = name;
		this.isVisible = isVisible;
		this.onVisibilityChanged = onVisibilityChanged;
	}

	public string Name { get; }

	public bool IsVisible
	{
		get => isVisible;
		set
		{
			if (isVisible == value)
			{
				return;
			}
			isVisible = value;
			Notify();
			onVisibilityChanged(this);
		}
	}

	/// Sets visibility without the change callback (bulk changes refilter once).
	public void SetVisibleSilently(bool value)
	{
		if (isVisible == value)
		{
			return;
		}
		isVisible = value;
		Notify(nameof(IsVisible));
	}

	public int Count
	{
		get => count;
		set
		{
			if (count == value)
			{
				return;
			}
			count = value;
			Notify();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	void Notify([CallerMemberName] string? property = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
	}
}
