using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Lib
{
	/// <summary>
	/// マテリアルとXAML等のUIコンポーネントを接続するアダプタ
	/// </summary>
	public class MaterialAdaptor : ViewModelBase
	{
		Material material_;
		IList<MaterialParam> parameterList_;
		IList<TextureFileParam> psTextureList_;

		public Material Material
		{
			get { return material_; }
			set {
				if (ParameterList != null) {
					// デリゲート解除
					foreach (var p in ParameterList) {
						p.Value.PropertyChanged -= Parameter_PropertyChanged;
					}
				}
				if (PSTextureList != null) {
					foreach (var p in PSTextureList) {
						p.PropertyChanged -= Parameter_PropertyChanged;
					}
				}

				material_ = value;
				if (material_ != null) {
					ParameterList = material_.userParams_;
					PSTextureList = material_.textureFileParams_;

					foreach (var p in ParameterList) {
						p.Value.PropertyChanged += Parameter_PropertyChanged;
					}
					foreach (var p in PSTextureList) {
						p.PropertyChanged += Parameter_PropertyChanged;
					}
				} else {
					ParameterList = null;
					PSTextureList = null;
				}
				OnPropertyChanged("Material");
			}
		}

		public IList<MaterialParam> ParameterList
		{ 
			get { return parameterList_; }
			set
			{
				parameterList_ = value;
				OnPropertyChanged("ParameterList");
			}
		}
		public IList<TextureFileParam> PSTextureList
		{
			get { return psTextureList_; }
			set
			{
				psTextureList_ = value;
				OnPropertyChanged("PSTextureList");
			}
		}

		public MaterialAdaptor()
		{
		}


		void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			OnPropertyChanged(e.PropertyName);
		}
	}
}
