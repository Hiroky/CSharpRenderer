using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Serialization;

using SlimDX;

namespace Lib
{
	/// <summary>
	/// マテリアルパラメータ
	/// </summary>
	[SerializableAttribute()]
	[XmlTypeAttribute(AnonymousType = true)]
	public class MaterialParam : ViewModelBase
	{
		[XmlIgnore]
		public string name_;
		[XmlIgnore]
		public ViewModelBase value_;

		[XmlAttribute("Name")]
		public string Name
		{
			get { return name_; }
			set { name_ = value; OnPropertyChanged("Name"); }
		}

		[XmlElement(ElementName = "Variable")]
		[XmlElement(typeof(MaterialParamValue<float>))]
		[XmlElement(typeof(MaterialParamValue<Color4>))]
		public ViewModelBase Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
	}

	[SerializableAttribute()]
	[XmlTypeAttribute(AnonymousType = true)]
	public class MaterialParamValue<T> : ViewModelBase
	{
		[XmlIgnore]
		T value_;

		[XmlElement(ElementName = "Value")]
		public T Value
		{
			get { return value_; }
			set { value_ = value; OnPropertyChanged("Value"); }
		}
	}


	/// <summary>
	/// テクスチャパラメータ
	/// </summary>
	[SerializableAttribute()]
	[XmlTypeAttribute(AnonymousType = true)]
	public class TextureFileParam : ViewModelBase
	{
		[XmlIgnore]
		string path_;

		[XmlIgnore]
		public Material Owner { get; set; }

		[XmlElement(ElementName = "Index")]
		public int Index { get; set; }

		[XmlElement(ElementName = "Path")]
		public string Path
		{
			get { return path_; }
			set { path_ = value; OnPropertyChanged("Path"); }
		}
	}



	/// <summary>
	/// マテリアルパラメータを生成する
	/// </summary>
	public class MaterialParamLoader
	{
		Material[] materials_;

		public MaterialParamLoader(Material[] materials)
		{
			materials_ = materials;
		}

		public void Load(string fileName)
		{
			foreach (var m in materials_) {
				m.userParams_ = new ObservableCollection<MaterialParam>();
				// 外部定義化
				m.userParams_.Add(new MaterialParam() { 
					name_ = "Roughness",
					value_ = new MaterialParamValue<float>() { Value = 0.3f }
				});
				m.userParams_.Add(new MaterialParam() { 
					name_ = "Reflectance",
					value_ = new MaterialParamValue<float>() { Value = 0.3f }
				});
				m.userParams_.Add(new MaterialParam() {
					name_ = "Reserve0",
					value_ = new MaterialParamValue<float>() { Value = 0.3f }
				});
				m.userParams_.Add(new MaterialParam() {
					name_ = "Reserve1",
					value_ = new MaterialParamValue<float>() { Value = 0.3f }
				});

				m.userParams_.Add(new MaterialParam() {
					name_ = "Albedo",
					value_ = new MaterialParamValue<SlimDX.Color4>() { Value = new SlimDX.Color4(1.0f, 1.0f, 1.0f, 1.0f) }
				});
				m.userParamSize_ = 4 * 8;
			}
		}
	}
}
