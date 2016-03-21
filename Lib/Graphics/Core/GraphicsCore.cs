using System.Windows.Forms;
using System.Windows.Controls;

using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SlimDX.Windows;
using DxDevice = SlimDX.Direct3D11.Device;
using DxDeviceContext = SlimDX.Direct3D11.DeviceContext;
using DxResource = SlimDX.Direct3D11.Resource;

namespace Lib
{
	/// <summary>
	/// レンダーステート定義
	/// </summary>
	public static class RenderState
	{
		/// <summary>
		/// ラスタライザ
		/// </summary>
		public enum RasterizerState : int
		{
			CullNone,		//カリングなし
			CullFront,		//表面カリング
			CullBack,		//裏面カリング
			WireFrame,		//ワイヤーフレーム描画

			Max,
		}

		/// <summary>
		/// ブレンド
		/// 基本的なもののみ
		/// </summary>
		public enum BlendState : int
		{
			None,
			Normal,
			Add,
			Subtruct,
			Multiply,

			Max,
		}

		/// <summary>
		/// デプス
		/// </summary>
		public enum DepthState :int
		{
			None,		//書き込まない、比較しない
			Normal,		//書き込むし、比較する
			TestOnly,	//比較だけする
			WriteOnly,	//書き込みのみする

			Max,
		}
	
		/// <summary>
		/// サンプラステート用
		/// サンプラはバリエーションが多すぎるのでテクスチャごとに持つとかする必要あるかも…
		/// </summary>
		public enum TextureAddressing : int
		{
			Clamp,
			Wrap,
			Mirror,
			Border0,

			Max,
		}


		public enum PrimitiveTopology : int
		{
			TriangleList,
			LineList,

			Max,
		}
	}


	/// <summary>
	/// グラフィックスコア機能
	/// </summary>
	public static class GraphicsCore
	{
		/// <summary>
		/// フレームバッファ
		/// </summary>
		public class FrameBuffer
		{
			/// <summary>
			/// カラーバッファ
			/// </summary>
			public Texture[] color_buffer_;

			/// <summary>
			/// デプスステンシル
			/// </summary>
			public Texture depth_stencil_;

			/// <summary>
			/// カラーバッファがアレイバッファか
			/// </summary>
			public bool is_array_buffer_;

			/// <summary>
			/// アレイバッファのインデックス
			/// </summary>
			public int array_buffer_index_;
		};

		static private System.IntPtr targetHandle_;
		static private SwapChain swapChain_;
		static private RenderTargetView defaultRenderTarget_;
		static private Texture defaultDepthStencil_;
		static private Texture defaultColorBuffer_;
		static private Query isFinishQuery_;
		static private Query timerDisjointQuery_;
		static private Query timerQuery_;

		// For WPF
		static Image wpfImage_;
		static private D3DImageSlimDX proxyImage_;

		// State
		static private RasterizerState[] rasterizerState_;
		static private BlendState[] blendState_;
		static private DepthStencilState[] depthStencilState_;
		static private SamplerState[][] samplerState_;
		static private RenderState.BlendState currentBlendState_ = RenderState.BlendState.Max;
		static private RenderState.DepthState currentDepthState_ = RenderState.DepthState.Max;
		static private RenderState.RasterizerState currentRasterizerState_ = RenderState.RasterizerState.Max;


		static internal DxDevice D3D11Device { get; set; }
		static public DxDeviceContext D3D11ImmediateContext { get; set; }	//TODO:最終的に隠蔽

		// TODO:内部管理はなくす
		static public Camera Camera3D { get; private set; }
		static public Camera Camera2D { get; private set; }
		static public Camera CurrentDrawCamera { get; set; }

		// 現状WPF用
		static public Texture DefaultColorBuffer { get { return defaultColorBuffer_; } }
		static public Texture DefaultDepthBuffer { get { return defaultDepthStencil_; } }

		static public int TargetWidth { get; private set; }
		static public int TargetHeight { get; private set; }

		// コンテキスト
		static public GraphicsContext ImmediateContext { get; private set; }

		// TODO:ライトマネージャを用意するべき
		static private Vector4 lightPos_;
		static private Vector4 viewSpaceLightPos_;
		static private bool isLightUpdate_;
		static public Vector4 LightPos { get { return lightPos_; } set { lightPos_ = value; isLightUpdate_ = true; } }
		static public Vector4 ViewSpaceLightPos { get { return viewSpaceLightPos_; } }


		#region Initialize / Finalize

		/// <summary>
		/// フォームを渡して初期化
		/// </summary>
		/// <param name="form"></param>
		static public void Initialize(RenderForm form)
		{
			Initialize(form.Handle, form.ClientSize.Width, form.ClientSize.Height);

			//// リサイズコールバック
			//form.UserResized += (o, e) => {
			//	ResizeTargetPanel(form.ClientSize.Width, form.ClientSize.Height);
			//};
		}

		/// <summary>
		/// コントロールから初期化
		/// </summary>
		/// <param name="control"></param>
		static public void Initialize(System.Windows.Forms.Control control)
		{
			Initialize(control.Handle, control.Width, control.Height);

			// リサイズコールバック
			//control.Resize += (o, e) => {
			//	ResizeTargetPanel(control.Width, control.Height);
			//};
		}

		/// <summary>
		/// ウインドウハンドルと初期矩形サイズから初期化
		/// (全初期化関数のベースとなる)
		/// </summary>
		/// <param name="handle"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		static public void Initialize(System.IntPtr handle, int width, int height)
		{
			DxDevice device;
			targetHandle_ = handle;
			var description = new SwapChainDescription() {
				BufferCount = 2,
				Usage = Usage.RenderTargetOutput,
				OutputHandle = handle,
				IsWindowed = true,
				ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
				SampleDescription = new SampleDescription(1, 0),
				Flags = SwapChainFlags.AllowModeSwitch,
				SwapEffect = SwapEffect.Discard
			};

#if DEBUG
			DeviceCreationFlags flags = DeviceCreationFlags.Debug;
#else
			DeviceCreationFlags flags = DeviceCreationFlags.None;
#endif

			// create Swapchain
			DxDevice.CreateWithSwapChain(DriverType.Hardware, flags, description, out device, out swapChain_);
			D3D11Device = device;

			// prevent DXGI handling of alt+enter, which doesn't work properly with Winforms
			using (var factory = swapChain_.GetParent<Factory>())
				factory.SetWindowAssociation(handle, WindowAssociationFlags.IgnoreAltEnter);

			// create a view of our render target, which is the backbuffer of the swap chain we just created
			using (var resource = DxResource.FromSwapChain<Texture2D>(swapChain_, 0))
				defaultRenderTarget_ = new RenderTargetView(device, resource);

			// setting a viewport is required if you want to actually see anything
			D3D11ImmediateContext = device.ImmediateContext;
			ImmediateContext = new GraphicsContext(D3D11ImmediateContext);

			var viewport = new Viewport(0.0f, 0.0f, width, height);
			TargetWidth = width;
			TargetHeight = height;

			// デプスバッファ
			defaultDepthStencil_ = new Texture(new Texture.InitDesc {
				width = width,
				height = height,
				//format = Format.R24G8_Typeless,
				format = Format.R32_Typeless,
				bindFlag = TextureBuffer.BindFlag.IsDepthStencil,
			});
			D3D11ImmediateContext.OutputMerger.SetTargets(defaultDepthStencil_.DepthStencilView, defaultRenderTarget_);
			D3D11ImmediateContext.Rasterizer.SetViewports(viewport);

			// トポロジーをトライアングルリスト固定でセットしておく
			D3D11ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

			// カメラの初期化
			Camera3D = new Camera();
			float aspect = (float)width / height;
			Camera3D.InitializePerspective(new Vector3(10, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), Math.DegToRad(45.0f), 0.1f, 1000.0f, aspect);
			CurrentDrawCamera = Camera3D;

			// レンダーステート
			InitializeRasterizerState();
			InitializeBlendState();
			InitializeDepthStencilState();
			InitializeSamplerState();

			InitializeQuery();
		}

		/// <summary>
		/// WPF用初期化
		/// </summary>
		/// <param name="img"></param>
		static public void InitializeForWPF(Image img)
		{
			//初期化
			int w, h;
			if (img.DesiredSize.Width == 0 || img.DesiredSize.Height == 0) {
				w = 100;
				h = 100;
			} else {
				w = (int)img.DesiredSize.Width;
				h = (int)img.DesiredSize.Height;
			}
			InitializeForWPF(w, h);

			// プロクシイメージ
			proxyImage_ = new D3DImageSlimDX();
			wpfImage_ = img;
			wpfImage_.Source = proxyImage_;
			//img.SizeChanged += (o, ea) => {
			//	Renderer.ResizeTargetPanel((int)img.DesiredSize.Width, (int)img.DesiredSize.Height);
			//};
			proxyImage_.SetBackBufferSlimDX((Texture2D)GraphicsCore.DefaultColorBuffer.TextureResource);
		}

		/// <summary>
		/// WPF用初期化
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		static public void InitializeForWPF(int width, int height)
		{
#if false
			Factory1 factory = new Factory1();
			Adapter adapter = GetAdapter(factory);
			//D3dDevice = new DxDevice(DriverType.Hardware, DeviceCreationFlags.None, FeatureLevel.Level_11_0);
			D3dDevice = new DxDevice(adapter, DeviceCreationFlags.None, FeatureLevel.Level_11_0);
			D3dDeviceContext = D3dDevice.ImmediateContext;
			factory.Dispose();
#else

#if DEBUG
			DeviceCreationFlags flags = DeviceCreationFlags.Debug;
#else
			DeviceCreationFlags flags = DeviceCreationFlags.None;
#endif
			D3D11Device = new DxDevice(DriverType.Hardware, flags, FeatureLevel.Level_11_0);
			D3D11ImmediateContext = D3D11Device.ImmediateContext;

#endif
			ImmediateContext = new GraphicsContext(D3D11ImmediateContext);

			// カラーバッファ
			defaultColorBuffer_ = new Texture(new Texture.InitDesc {
				width = width,
				height = height,
				//format = Format.R8G8B8A8_UNorm,
				format = Format.B8G8R8A8_UNorm,
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				optionFlags = ResourceOptionFlags.Shared,
			});
			defaultRenderTarget_ = defaultColorBuffer_.RenderTargetView;

			// デプスバッファ
			defaultDepthStencil_ = new Texture(new Texture.InitDesc {
				width = width,
				height = height,
				//format = Format.R24G8_Typeless,
				format = Format.R32_Typeless,
				bindFlag = TextureBuffer.BindFlag.IsDepthStencil,
			});

			var viewport = new Viewport(0.0f, 0.0f, width, height);
			TargetWidth = width;
			TargetHeight = height;
			D3D11ImmediateContext.OutputMerger.SetTargets(defaultDepthStencil_.DepthStencilView, defaultRenderTarget_);
			D3D11ImmediateContext.Rasterizer.SetViewports(viewport);

			// トポロジーをトライアングルリスト固定でセットしておく
			D3D11ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

			// カメラの初期化
			Camera3D = new Camera();
			float aspect = (float)width / height;
			Camera3D.InitializePerspective(new Vector3(10, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), Math.DegToRad(45.0f), 0.1f, 1000.0f, aspect);
			CurrentDrawCamera = Camera3D;

			// レンダーステート
			InitializeRasterizerState();
			InitializeBlendState();
			InitializeDepthStencilState();
			InitializeSamplerState();

			InitializeQuery();
		}

		/// <summary>
		/// 最適グラフィックスアダプタを返す
		/// </summary>
		/// <returns></returns>
		static Adapter GetAdapter(Factory1 factory)
		{
			// 優先順
			string[] vender_string = new string[] {
				"nvidia",
				"ati",
			};
			Adapter1 result = factory.GetAdapter1(0);
			int adapterCount = factory.GetAdapterCount1();
			for (int i = 1; i < adapterCount; i++) {
				Adapter1 adapter = factory.GetAdapter1(i);
				var device_name = adapter.Description.Description.ToLower();
				for (int j = 0; j < vender_string.Length; j++) {
					if (device_name.Contains(vender_string[j])) {
						result = adapter;
					}
				}
			}

			return result;
		}

		/// <summary>
		/// ターゲットがリサイズされたときに呼ぶ必要がある
		/// </summary>
		static public void ResizeTargetPanel(int width, int height)
		{
			if (width == 0 || height == 0) {
				return;
			}

			// 現状原因がわかってないけれどもdockしている状態で閉じると例外が発生するので…
			try {
				D3D11ImmediateContext.OutputMerger.SetTargets(new RenderTargetView[] { });

				// バックバッファを作り替える
				if (swapChain_ != null) {
					defaultRenderTarget_.Dispose();
					swapChain_.ResizeBuffers(2, 0, 0, Format.R8G8B8A8_UNorm, SwapChainFlags.AllowModeSwitch);
					using (var resource = DxResource.FromSwapChain<Texture2D>(swapChain_, 0))
						defaultRenderTarget_ = new RenderTargetView(D3D11Device, resource);
				} else {
					// TODO:新たにnewしてるのでこれだとテクスチャとしてこれを参照してた場合リンクが切れる…。
					defaultColorBuffer_.Dispose();
					//defaultColorBuffer_ = new Texture(new Texture.InitDesc {
					defaultColorBuffer_.Initialize(new Texture.InitDesc {
						width = width,
						height = height,
						//format = Format.R8G8B8A8_UNorm,
						format = Format.B8G8R8A8_UNorm,
						bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
						optionFlags = ResourceOptionFlags.Shared,
					});
					defaultRenderTarget_ = defaultColorBuffer_.RenderTargetView;
				}

				// デプスステンシル
				defaultDepthStencil_.Dispose();
				//defaultDepthStencil_ = new Texture(new Texture.InitDesc {
				defaultDepthStencil_.Initialize(new Texture.InitDesc {
					width = width,
					height = height,
					//format = Format.R24G8_Typeless,
					format = Format.R32_Typeless,
					bindFlag = TextureBuffer.BindFlag.IsDepthStencil,
				});

				// プロクシイメージ
				if (proxyImage_ != null) {
					proxyImage_.SetBackBufferSlimDX((Texture2D)GraphicsCore.DefaultColorBuffer.TextureResource);
				}

				D3D11ImmediateContext.OutputMerger.SetTargets(defaultDepthStencil_.DepthStencilView, defaultRenderTarget_);

				// ビューポートを再セット(フルスクリーンビューポート)
				var new_viewport = new Viewport(0.0f, 0.0f, width, height);
				D3D11ImmediateContext.Rasterizer.SetViewports(new_viewport);
				TargetWidth = width;
				TargetHeight = height;

				// カメラ再セット(仮)
				Camera3D.SetAspect((float)width / height);
				Camera3D.Update();
			} catch {
			}
		}

		/// <summary>
		/// クエリ初期化
		/// </summary>
		static void InitializeQuery()
		{
			QueryDescription desc = new QueryDescription(QueryType.Event, QueryFlags.None);
			isFinishQuery_ = new Query(D3D11Device, desc);

			desc = new QueryDescription(QueryType.TimestampDisjoint, QueryFlags.None);
			timerDisjointQuery_ = new Query(D3D11Device, desc);
			desc = new QueryDescription(QueryType.Timestamp, QueryFlags.None);
			timerQuery_ = new Query(D3D11Device, desc);
		}

		/// <summary>
		/// ラスタライザステート
		/// </summary>
		static void InitializeRasterizerState()
		{
			var device = D3D11Device;

			rasterizerState_ = new RasterizerState[(int)RenderState.RasterizerState.Max];

			// CullBack
			RasterizerStateDescription rsStateDesc = new RasterizerStateDescription() {
				CullMode = CullMode.Back,
				IsFrontCounterclockwise = true,
				FillMode = FillMode.Solid,
				IsDepthClipEnabled = true
			};
			rasterizerState_[(int)RenderState.RasterizerState.CullBack] = RasterizerState.FromDescription(device, rsStateDesc);

			// CullFront
			rsStateDesc = new RasterizerStateDescription() {
				CullMode = CullMode.Front,
				IsFrontCounterclockwise = true,
				FillMode = FillMode.Solid,
				IsDepthClipEnabled = true
			};
			rasterizerState_[(int)RenderState.RasterizerState.CullFront] = RasterizerState.FromDescription(device, rsStateDesc);

			// CullNone
			rsStateDesc = new RasterizerStateDescription() {
				CullMode = CullMode.None,
				IsFrontCounterclockwise = true,
				FillMode = FillMode.Solid,
				IsDepthClipEnabled = true
			};
			rasterizerState_[(int)RenderState.RasterizerState.CullNone] = RasterizerState.FromDescription(device, rsStateDesc);

			// WireFrame
			rsStateDesc = new RasterizerStateDescription() {
				CullMode = CullMode.Back,
				IsFrontCounterclockwise = true,
				FillMode = FillMode.Wireframe,
				IsDepthClipEnabled = true
			};
			rasterizerState_[(int)RenderState.RasterizerState.WireFrame] = RasterizerState.FromDescription(device, rsStateDesc);

			// 初期値
			SetRasterizerState(RenderState.RasterizerState.CullBack);
		}

		/// <summary>
		/// ブレンドステート
		/// </summary>
		static void InitializeBlendState()
		{
			var device = D3D11Device;

			blendState_ = new BlendState[(int)RenderState.BlendState.Max];

			// None
			BlendStateDescription desc = new BlendStateDescription() {
				IndependentBlendEnable = false,
				AlphaToCoverageEnable = false,
			};
			desc.RenderTargets[0].BlendEnable = false;
			desc.RenderTargets[0].SourceBlend = BlendOption.One;
			desc.RenderTargets[0].DestinationBlend = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperation = BlendOperation.Add;
			desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
			desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
			desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
			blendState_[(int)RenderState.BlendState.None] = BlendState.FromDescription(device, desc);

			// Normal
			desc.RenderTargets[0].BlendEnable = true;
			desc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
			desc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
			desc.RenderTargets[0].BlendOperation = BlendOperation.Add;
			desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
			desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
			desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
			blendState_[(int)RenderState.BlendState.Normal] = BlendState.FromDescription(device, desc);

			// Add
			desc.RenderTargets[0].BlendEnable = true;
			desc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
			desc.RenderTargets[0].DestinationBlend = BlendOption.One;
			desc.RenderTargets[0].BlendOperation = BlendOperation.Add;
			desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
			desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
			desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
			blendState_[(int)RenderState.BlendState.Add] = BlendState.FromDescription(device, desc);

			// Subtruct
			desc.RenderTargets[0].BlendEnable = true;
			desc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
			desc.RenderTargets[0].DestinationBlend = BlendOption.One;
			desc.RenderTargets[0].BlendOperation = BlendOperation.Subtract;
			desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
			desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
			desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
			blendState_[(int)RenderState.BlendState.Subtruct] = BlendState.FromDescription(device, desc);

			// Multiply
			desc.RenderTargets[0].BlendEnable = true;
			desc.RenderTargets[0].SourceBlend = BlendOption.Zero;
			desc.RenderTargets[0].DestinationBlend = BlendOption.SourceColor;
			desc.RenderTargets[0].BlendOperation = BlendOperation.Add;
			desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
			desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
			desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
			desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
			blendState_[(int)RenderState.BlendState.Multiply] = BlendState.FromDescription(device, desc);

			// 初期値
			SetBlendState(RenderState.BlendState.None);
		}

		/// <summary>
		/// デプスステンシルステート
		/// </summary>
		static void InitializeDepthStencilState()
		{
			var device = D3D11Device;

			depthStencilState_ = new DepthStencilState[(int)RenderState.DepthState.Max];

			// None
			DepthStencilStateDescription dsStateDesc = new DepthStencilStateDescription() {
				IsDepthEnabled = false,
				IsStencilEnabled = false,
				DepthWriteMask = DepthWriteMask.Zero,
				DepthComparison = Comparison.Less,
			};
			depthStencilState_[(int)RenderState.DepthState.None] = DepthStencilState.FromDescription(device, dsStateDesc);

			// Normal
			dsStateDesc = new DepthStencilStateDescription() {
				IsDepthEnabled = true,
				IsStencilEnabled = false,
				DepthWriteMask = DepthWriteMask.All,
				DepthComparison = Comparison.Less,
			};
			depthStencilState_[(int)RenderState.DepthState.Normal] = DepthStencilState.FromDescription(device, dsStateDesc);

			// TestOnly
			dsStateDesc = new DepthStencilStateDescription() {
				IsDepthEnabled = true,
				IsStencilEnabled = false,
				DepthWriteMask = DepthWriteMask.Zero,
				DepthComparison = Comparison.Less,
			};
			depthStencilState_[(int)RenderState.DepthState.TestOnly] = DepthStencilState.FromDescription(device, dsStateDesc);

			// WriteOnly
			dsStateDesc = new DepthStencilStateDescription() {
				IsDepthEnabled = false,
				IsStencilEnabled = false,
				DepthWriteMask = DepthWriteMask.Zero,
				DepthComparison = Comparison.Less,
			};
			depthStencilState_[(int)RenderState.DepthState.WriteOnly] = DepthStencilState.FromDescription(device, dsStateDesc);

			// 初期値
			SetDepthState(RenderState.DepthState.Normal);
		}

		/// <summary>
		/// サンプラステート
		/// 現状フィルタはリニア固定
		/// </summary>
		static void InitializeSamplerState()
		{
			var device = D3D11Device;
			int state_num = (int)RenderState.TextureAddressing.Max;

			samplerState_ = new SamplerState[state_num][];

			TextureAddressMode[] dx_list = new TextureAddressMode[] {
				TextureAddressMode.Clamp, 
				TextureAddressMode.Wrap,
				TextureAddressMode.Mirror,
				TextureAddressMode.Border,
			};

			for (int i = 0; i < state_num; i++) {
				samplerState_[i] = new SamplerState[state_num];
				for (int j = 0; j < state_num; j++) {
					SamplerDescription samplerDescription = new SamplerDescription() {
						AddressU = dx_list[i],
						AddressV = dx_list[j],
						AddressW = TextureAddressMode.Clamp,
						Filter = Filter.MinMagMipLinear,
						//Filter = Filter.MinMagMipPoint,
						//Filter = Filter.Anisotropic,		//とりあえず固定でAnisoに
						MipLodBias = -1,
						MinimumLod = 0,
						MaximumLod = 255,
						MaximumAnisotropy = 4,
						BorderColor = new Color4(0, 0, 0, 1),
					};

					samplerState_[i][j] = SamplerState.FromDescription(device, samplerDescription);
				}
			}


			// 初期値
			int wrap = (int)RenderState.TextureAddressing.Wrap;
			D3D11ImmediateContext.PixelShader.SetSampler(samplerState_[wrap][wrap], 0);
		}

		/// <summary>
		/// 終了処理
		/// </summary>
		static public void Dispose()
		{
			if (isFinishQuery_ != null) {
				isFinishQuery_.Dispose();
			}
			if (timerDisjointQuery_ != null) {
				timerDisjointQuery_.Dispose();
			}
			if (timerQuery_ != null) {
				timerQuery_.Dispose();
			}
			foreach (var s in rasterizerState_) {
				s.Dispose();
			}
			foreach (var s in blendState_) {
				s.Dispose();
			}
			foreach (var s in depthStencilState_) {
				s.Dispose();
			}
			foreach (var ary in samplerState_) {
				foreach (var s in ary) {
					s.Dispose();
				}
			}
			if (defaultColorBuffer_ != null) {
				defaultColorBuffer_.Dispose();
			} else {
				defaultRenderTarget_.Dispose();
			}
			defaultDepthStencil_.Dispose();
			if (swapChain_ != null) {
				swapChain_.Dispose(); 
			}
			if (proxyImage_ != null) {
				proxyImage_.Dispose();
			}
			D3D11Device.Dispose();
		}

		#endregion

		/// <summary>
		/// カメラ、ライトの更新が行われます
		/// </summary>
		static public void Update()
		{
			Camera3D.Update();

			if (Camera3D.IsUpdated || isLightUpdate_) {
				viewSpaceLightPos_ = Vector4.Transform(lightPos_, Camera3D.ViewMatrix);
				isLightUpdate_ = false;
			}
		}

		/// <summary>
		/// スワップ
		/// </summary>
		static public void Present()
		{
			if (swapChain_ != null) {
				D3D11ImmediateContext.End(isFinishQuery_);
				swapChain_.Present(1, PresentFlags.None);
			} else {
				// フラッシュ(Presentがないのでフラッシュが必要)
				D3D11ImmediateContext.End(isFinishQuery_);
				D3D11ImmediateContext.Flush();
				if (proxyImage_ != null) proxyImage_.InvalidateD3DImage();
			}
		}

		/// <summary>
		/// デフォルトのバッファをバインド
		/// </summary>
		static public void BeginRender()
		{
			D3D11ImmediateContext.OutputMerger.SetTargets(defaultDepthStencil_.DepthStencilView, defaultRenderTarget_);

			var viewport = new Viewport(0.0f, 0.0f, TargetWidth, TargetHeight);
			D3D11ImmediateContext.Rasterizer.SetViewports(viewport);
		}

		/// <summary>
		/// ブレンドステート設定
		/// </summary>
		/// <param name="state"></param>
		static public void SetBlendState(RenderState.BlendState state)
		{
			//if (currentBlendState_ != state) 
			{
				D3D11ImmediateContext.OutputMerger.BlendState = blendState_[(int)state];
				currentBlendState_ = state;
			}
		}

		/// <summary>
		/// デプスステート設定
		/// </summary>
		/// <param name="state"></param>
		static public void SetDepthState(RenderState.DepthState state)
		{
			if (currentDepthState_ != state) {
				D3D11ImmediateContext.OutputMerger.DepthStencilState = depthStencilState_[(int)state];
				currentDepthState_ = state;
			}
		}

		/// <summary>
		/// ラスタライザステート設定
		/// </summary>
		/// <param name="state"></param>
		static public void SetRasterizerState(RenderState.RasterizerState state)
		{
			if (currentRasterizerState_ != state) {
				D3D11ImmediateContext.Rasterizer.State = rasterizerState_[(int)state];
				currentRasterizerState_ = state;
			}
		}

		/// <summary>
		/// ステートキャッシュクリア
		/// </summary>
		static public void InvalidateStateCache()
		{
			currentBlendState_ = RenderState.BlendState.Max;
			currentDepthState_ = RenderState.DepthState.Max;
			currentRasterizerState_ = RenderState.RasterizerState.Max;
		}

		/// <summary>
		/// サンプラー設定
		/// </summary>
		/// <param name="index"></param>
		/// <param name="u"></param>
		/// <param name="v"></param>
		static public void SetSamplerStatePS(int index, RenderState.TextureAddressing u, RenderState.TextureAddressing v)
		{
			D3D11ImmediateContext.PixelShader.SetSampler(samplerState_[(int)u][(int)v], index);
		}
		static public void SetSamplerStateVS(int index, RenderState.TextureAddressing u, RenderState.TextureAddressing v)
		{
			D3D11ImmediateContext.VertexShader.SetSampler(samplerState_[(int)u][(int)v], index);
		}
		static public void SetSamplerStateCS(int index, RenderState.TextureAddressing u, RenderState.TextureAddressing v)
		{
			D3D11ImmediateContext.ComputeShader.SetSampler(samplerState_[(int)u][(int)v], index);
		}
	}
}
