using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using ConbinerEditor.ViewModel;

namespace Renderer
{
	/// <summary>
	/// 
	/// </summary>
	public class ProfileObject : ViewModelBase
	{
		string name_;
		float time_;

		public string Name { 
			get { return name_; }
			set { 
				name_ = value;
				OnPropertyChanged("Name");
			}
		}

		public float Time {
			get { return time_; }
			set
			{
				time_ = value;
				OnPropertyChanged("Time");
			}
		}
	}


	/// <summary>
	/// 
	/// </summary>
	public class MainWindowViewModel : ViewModelBase, IDisposable
	{
		ObservableCollection<PropertyItem> properties_;
		bool isEnableFXAA_;

		ObservableCollection<ProfileObject> profileObjectList_;
		PropertyItemValueEnum viewIndex_;
		PropertyItemValueEnum tiledRenderView_;
		PropertyItemValueInt lightCount_;
		PropertyItemValue<bool> isDrawLights_;
		PropertyItemValue<bool> isEnableToneMap_;
		PropertyItemValue<bool> isEnableProfile_;
		PropertyItemValueFloat toneMapKeyValue_;

		#region property
		public bool IsEnableFXAA
		{
			get { return isEnableFXAA_; }
			set
			{
				isEnableFXAA_ = value;
				OnPropertyChanged("IsEnableFXAA");
			}
		}
		public int ViewIndex
		{
			get { return viewIndex_.Value; }
			set
			{
				viewIndex_.Value = value;
				OnPropertyChanged("ViewIndex");
			}
		}
		public int TiledRenderView
		{
			get { return tiledRenderView_.Value; }
			set
			{
				tiledRenderView_.Value = value;
				IsTiledRenderViewChanged = true;
				OnPropertyChanged("TiledRenderView");
			}
		}
		public int LightCount
		{
			get { return lightCount_.Value; }
			set
			{
				lightCount_.Value = value;
				OnPropertyChanged("LightCount");
			}
		}
		public bool IsDrawLights
		{
			get { return isDrawLights_.Value; }
			set
			{
				isDrawLights_.Value = value;
				OnPropertyChanged("IsDrawLights");
			}
		}
		public bool IsEnableToneMap
		{
			get { return isEnableToneMap_.Value; }
			set
			{
				isEnableToneMap_.Value = value;
				OnPropertyChanged("IsEnableToneMap");
			}
		}
		public float ToneMapKeyValue
		{
			get { return toneMapKeyValue_.Value; }
			set
			{
				toneMapKeyValue_.Value = value;
				OnPropertyChanged("ToneMapKeyValue");
			}
		}
		public ObservableCollection<ProfileObject> ProfileObjectList
		{
			get { return profileObjectList_; }
		}
		public bool IsEnableProfile
		{
			get { return isEnableProfile_.Value; }
			set
			{
				isEnableProfile_.Value = value;
				OnPropertyChanged("IsEnableProfile");
			}
		}
		public ObservableCollection<PropertyItem> Properties
		{
			get { return properties_; }
		}

		public bool IsTiledRenderViewChanged { get; set; }
		#endregion

		/// <summary>
		/// 
		/// </summary>
		public MainWindowViewModel()
		{
			profileObjectList_ = new ObservableCollection<ProfileObject>();
	
			// 調整項目
			properties_ = new ObservableCollection<PropertyItem>();
			viewIndex_ = new PropertyItemValueEnum(0, new ViewItems().Items);
			tiledRenderView_ = new PropertyItemValueEnum(0, new TiledRenderItems().Items);
			lightCount_ = new PropertyItemValueInt(1024, 0, 2048);
			isDrawLights_ = new PropertyItemValue<bool>(true);
			isEnableToneMap_ = new PropertyItemValue<bool>(true);
			toneMapKeyValue_ = new PropertyItemValueFloat(0.2f, 0, 1);
			isEnableProfile_ = new PropertyItemValue<bool>(true);
			properties_.Add(new PropertyItem() { name_ = "表示", value_ = viewIndex_ });
			properties_.Add(new PropertyItem() { name_ = "Deferred", value_ = tiledRenderView_ });
			properties_.Add(new PropertyItem() { name_ = "ライト数", value_ = lightCount_ });
			properties_.Add(new PropertyItem() { name_ = "ライト描画", value_ = isDrawLights_ });
			properties_.Add(new PropertyItem() { name_ = "トーンマップ", value_ = isEnableToneMap_ });
			properties_.Add(new PropertyItem() { name_ = "トーンマップ.KeyValue", value_ = toneMapKeyValue_ });

			IsEnableFXAA = true;
			IsEnableProfile = true;
		}

		public void Dispose()
		{
		}
	}
}
