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

using System.Windows.Forms;
using System.Windows.Interop;

using Lib;
using SlimDX;
using SlimDX.Direct3D11;

using DXMatrix = SlimDX.Matrix;
using GraphicsCore = Lib.GraphicsCore;
using MyGPUProfiler = Lib.Ext.GPUProfiler;

namespace Renderer
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		IScene scene_;
		int profileCounter_;
		Task renderTask_;
		bool isRun_;

		/// <summary>
		/// 
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();

			profileCounter_ = 0;

			// イベント登録
			Loaded += new RoutedEventHandler(Window_Loaded);
			Closing += new CancelEventHandler(Window_Closing);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Window_Loaded(object sender, RoutedEventArgs e)
		{
			InitializeRenderer();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			isRun_ = false;
			renderTask_.Wait();

			MyGPUProfiler.Dispose();
			scene_.Dispose();
			ShaderManager.Dispose();
			GraphicsCore.Dispose();
		}


		/// <summary>
		/// 
		/// </summary>
		public void InitializeRenderer()
		{
#if true
			GraphicsCore.Initialize(renderCtrl);
			Input.Initialize(renderCtrl);
#else
			int width = (int)RenderImage.DesiredSize.Width;
			int height = (int)RenderImage.DesiredSize.Height;
			width = (width < 128) ? 128 : width;
			height = (height < 128) ? 128 : height;
			GraphicsCore.InitializeForWPF(RenderImage);
			Input.Initialize(RenderImage);
#endif
			// シェーダ
			ShaderManager.Initialize("asset/shader/shader.lst");
			ShaderManager.DefaultShader = "Unlit";

			// プロファイラ
			MyGPUProfiler.Initialize();

			// ライト
			GraphicsCore.LightPos = new Vector4(0, 100000, 0, 1);

			// シーン
			scene_ = new GameScene();
			//scene_ = new TestScene();
			DataContext = scene_.ViewModel;

#if true
			// リサイズコールバック
			bool requireResize = false;
			renderCtrl.SizeChanged += (o, e) => {
				if (renderCtrl.Width == 0 || renderCtrl.Height == 0) {
					return;
				}
				requireResize = true;
			};

			// レンダースレッド
			isRun_ = true;
			renderTask_ = new Task(() => {
				while (isRun_) {
					// リサイズ
					if (requireResize) {
						GraphicsCore.ResizeTargetPanel((int)renderCtrl.Width, (int)renderCtrl.Height);
						scene_.ScreenSizeChanged((int)renderCtrl.Width, (int)renderCtrl.Height);
						requireResize = false;
					}

					// 描画
					var context = GraphicsCore.ImmediateContext;
					scene_.Update();
					MyGPUProfiler.BeginFrameProfiling(context);
					scene_.Draw();
					MyGPUProfiler.EndFrameProfiling(context);
					GraphicsCore.Present();
					UpdateProfiler();
				};
			});
			renderTask_.Start();
#else
			// リサイズコールバック
			RenderImage.SizeChanged += (o, e) => {
				if (e.NewSize.Width == 0 || e.NewSize.Height == 0) {
					return;
				}
				GraphicsCore.ResizeTargetPanel((int)e.NewSize.Width, (int)e.NewSize.Height);
				scene_.ScreenSizeChanged((int)e.NewSize.Width, (int)e.NewSize.Height);
			};
			// 初回描画後に一回テクスチャを更新
			ContentRendered += (o, e) => {
				GraphicsCore.ResizeTargetPanel((int)RenderImage.DesiredSize.Width, (int)RenderImage.DesiredSize.Height);
				scene_.ScreenSizeChanged((int)RenderImage.DesiredSize.Width, (int)RenderImage.DesiredSize.Height);
			};

			// 描画コールバック設定
			//ComponentDispatcher.ThreadIdle += (o, e) => {
			CompositionTarget.Rendering += (o, e) => {
				scene_.Update();
				MyGPUProfiler.BeginFrameProfiling(GraphicsCore.D3dCurrentContext);
				scene_.Draw();
				MyGPUProfiler.EndFrameProfiling(GraphicsCore.D3dCurrentContext);
				GraphicsCore.Present();
				UpdateProfiler();
			};
#endif
		}


		/// <summary>
		/// プロファイラ更新
		/// </summary>
		void UpdateProfiler()
		{
			if (!scene_.ViewModel.IsEnableProfile) return;

			if (profileCounter_ == 0) {
				var list = scene_.ViewModel.ProfileObjectList;
				list.Clear();

				if (MyGPUProfiler.m_CurrentFrameProfilerTree != null) {
					Action<MyGPUProfiler.ProfilerTreeMember, String> processLevel = null;
					processLevel = (MyGPUProfiler.ProfilerTreeMember treeMember, String level) => {
						string finalName = level + treeMember.m_Name;
						var obj = new ProfileObject();
						obj.Name = finalName;
						obj.Time = (float)treeMember.m_Time;
						list.Add(obj);
						foreach (var v in treeMember.m_ChildMembers) {
							processLevel(v, level + "  ");
						}
					};
					processLevel(MyGPUProfiler.m_CurrentFrameProfilerTree, "");
				}
				profileCounter_ = 30;
				return;
			}
			profileCounter_--;
		}


		/// <summary>
		/// キューブマップ読み込み
		/// </summary>
		/// <param name="file_name"></param>
		public void LoadSkyImage(string file_name)
		{
		}


		/// <summary>
		/// モデル読み込み
		/// </summary>
		/// <param name="file_name"></param>
		public void LoadModel(string file_name)
		{
			//scene_.LoadModel(file_name);
		}


		private void ModelLoadMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFileDialog();
			dialog.Filter = "モデルファイル(*.mml,*.obj,*.dae)|*.mml;*.obj;*.dae";
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				LoadModel(dialog.FileName);
			}
		}

		private void LoadSkyboxMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFileDialog();
			dialog.Filter = "キューブマップ(*.dds)|*.dds";
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				LoadSkyImage(dialog.FileName);
			}
		}


		private static int UIColorToDXColor(System.Windows.Media.Color c)
		{
			return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
		}


		private void LoadShaderMenuItem_Click(object sender, RoutedEventArgs e)
		{
			ShaderManager.Reload("asset/shader/shader.lst");
		}
	}
}
