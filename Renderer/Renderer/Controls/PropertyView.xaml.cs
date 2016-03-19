using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

using Lib;

namespace Renderer
{
	/// <summary>
	/// PropertyView.xaml の相互作用ロジック
	/// </summary>
	public partial class PropertyView : UserControl
	{
		public PropertyView()
		{
			InitializeComponent();
		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{

		}
	}



	/// <summary>
	/// コンバータ
	/// </summary>
	[System.Windows.Data.ValueConversion(typeof(SlimDX.Color4), typeof(System.Windows.Media.Color))]
	public class ColorValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			SlimDX.Color4 color = (SlimDX.Color4)value;
			return System.Windows.Media.Color.FromScRgb(color.Alpha, color.Red, color.Green, color.Blue);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			System.Windows.Media.Color color = (System.Windows.Media.Color)value;

			return new SlimDX.Color4(color.ScA, color.ScR, color.ScG, color.ScB);
		}
	}


	/// <summary>
	/// テンプレートセレクタ
	/// </summary>
	public class PropertyViewTemplateSelector : System.Windows.Controls.DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item != null) {
				FrameworkElement element = container as FrameworkElement;
				if (item is PropertyItemValueFloat) {
					return element.FindResource("FloatItemTemplate") as DataTemplate;
				} else if (item is PropertyItemValueInt) {
					return element.FindResource("IntItemTemplate") as DataTemplate;
				} else if (item is PropertyItemValueEnum) {
					return element.FindResource("EnumItemTemplate") as DataTemplate;
				} else if (item is PropertyItemValue<bool>) {
					return element.FindResource("BoolItemTemplate") as DataTemplate;
				} else if (item is PropertyItemValue<SlimDX.Color4>) {
					return element.FindResource("ColorItemTemplate") as DataTemplate;
				}
			}
			return null;
		}
	}


	/// <summary>
	/// 
	/// </summary>
	public class PropertyItem : ViewModelBase
	{
		public string name_;
		public ViewModelBase value_;

		public string Name
		{
			get { return name_; }
			set { name_ = value; OnPropertyChanged("Name"); }
		}

		public ViewModelBase Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class PropertyItemValue<T> : ViewModelBase
	{
		T value_;

		public PropertyItemValue()
		{
		}

		public PropertyItemValue(T value)
		{
			value_ = value;
		}

		public T Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public class PropertyItemValueFloat : ViewModelBase
	{
		float value_;

		public PropertyItemValueFloat()
		{
		}

		public PropertyItemValueFloat(float value, float min = 0, float max = 1)
		{
			value_ = value;
			Min = min;
			Max = max;
		}

		public float Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
		public float Min { get; set; }
		public float Max { get; set; }
	}

	/// <summary>
	/// 
	/// </summary>
	public class PropertyItemValueInt : ViewModelBase
	{
		int value_;

		public PropertyItemValueInt()
		{
		}

		public PropertyItemValueInt(int value, int min = 0, int max = 100)
		{
			value_ = value;
			Min = min;
			Max = max;
		}

		public int Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
		public int Min { get; set; }
		public int Max { get; set; }
	}

	/// <summary>
	/// 
	/// </summary>
	public class PropertyItemValueEnum : ViewModelBase
	{
		int value_;

		public PropertyItemValueEnum()
		{
		}

		public PropertyItemValueEnum(int value, EnumItem[] items = null)
		{
			value_ = value;
			Items = items;
		}

		public int Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
		public IEnumerable<EnumItem> Items { get; set; }
	}

	/// <summary>
	/// コンボボックスアイテム
	/// </summary>
	public class EnumItem : INotifyPropertyChanged
	{
		int id_;
		public int ID
		{
			get { return id_; }
			set
			{
				id_ = value;
				OnPropertyChanged("ID");
			}
		}

		string content_;
		public string Content
		{
			get { return content_; }
			set
			{
				content_ = value;
				OnPropertyChanged("Content");
			}
		}

		public EnumItem()
		{
		}

		public EnumItem(int id, string content)
		{
			id_ = id;
			content_ = content;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
