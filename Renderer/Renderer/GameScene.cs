using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lib;
using SlimDX;
using SlimDX.Direct3D11;
using DXMatrix = SlimDX.Matrix;
using ksRenderer = Lib.Renderer;
using ksGpuProfilePoint = Lib.Ext.GpuProfilePoint;


namespace Renderer
{
	struct CubeMapGenInfo
	{
		public Vector3 boundingMin;
		public Vector3 boundingMax;
		public int numX;
		public int numY;
		public int numZ;
	}

	class GameScene : IDisposable, IScene
	{
		Vector3 camera_at_ = new Vector3(0, 5, 0);
		CameraController cameraCtrl_;

		ComputeManager csMgr_;
		LightManager lightMgr_;
		ShadowMap shadowMap_;
		ToneMap toneMap_;

		Texture[] gbuffers_;
		Texture.InitDesc[] gbufferDesc_;
		Texture.InitDesc uavDesc_;
		Texture uav_;
		Texture.InitDesc hdrResolveDesc_;
		Texture hdrResolveBuffer_;
		ksRenderer.FrameBuffer deferredBuffer_;
		ksRenderer.FrameBuffer forwardBuffer_;
		ksRenderer.FrameBuffer hdrResultBuffer_;

		ReflectionCapture globalCapture_;
		List<ReflectionCapture> localCapture_;
		Texture[] giTextures_;
		CubeMapGenInfo cubeMapInfo_;
		bool cubeMapRendered_;
		bool cubeMapRenderEnable_;

		Prim prim_;
		Prim fxaaPrim_;

		Model drawModel_;

		Texture[] debugViewList_;

		MainWindowViewModel viewModel_;
		public MainWindowViewModel ViewModel { get { return viewModel_; } }

		/// <summary>
		/// 
		/// </summary>
		public GameScene()
		{
			viewModel_ = new MainWindowViewModel();
			viewModel_.LightCount = 2048;

			lightMgr_ = new LightManager(viewModel_.LightCount);
			toneMap_ = new ToneMap();
			shadowMap_ = new ShadowMap(1024, 1024, ShadowMap.Type.VSM);
			csMgr_ = new ComputeManager();
			csMgr_.ShadowMap = shadowMap_;
			csMgr_.LightManager = lightMgr_;

			// シャドウマップの撮影設定
			Vector3 lightPos = -lightMgr_.DirectionalLightDir * 50;
			shadowMap_.Camera.SetPosition(ref lightPos);
			shadowMap_.Camera.Update();

			// カメラ操作
			ksRenderer.Camera3D.SetAt(ref camera_at_);
			cameraCtrl_ = new CameraController(ksRenderer.Camera3D);

			// キューブマップ撮影
			cubeMapRendered_ = false;
			cubeMapInfo_ = new CubeMapGenInfo() {
				numX = 4,
				numY = 16,
				numZ = 9,
				boundingMin = new Vector3(-28, 1, -12),
				boundingMax = new Vector3(28, 30, 12),
			};
			cubeMapRenderEnable_ = true;
			csMgr_.CubeMapInfo = cubeMapInfo_;

			InitializeTarget();
			InitialzeSceneObject();
		}

		/// <summary>
		/// レンダーターゲット初期化
		/// </summary>
		void InitializeTarget()
		{
			// deferred用MRTバッファ
			gbufferDesc_ = new Texture.InitDesc[] {
				new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = ksRenderer.TargetWidth,
					height = ksRenderer.TargetHeight,
					format = SlimDX.DXGI.Format.R16G16B16A16_Float,
				},
				new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = ksRenderer.TargetWidth,
					height = ksRenderer.TargetHeight,
					format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
				},
				new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = ksRenderer.TargetWidth,
					height = ksRenderer.TargetHeight,
					format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
				},
			};
			gbuffers_ = new Texture[gbufferDesc_.Length];
			for (int i = 0; i < gbuffers_.Length; i++) {
				gbuffers_[i] = new Texture(gbufferDesc_[i]);
			}
			deferredBuffer_ = new ksRenderer.FrameBuffer();
			deferredBuffer_.color_buffer_ = gbuffers_;
			deferredBuffer_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;

			uavDesc_ = new Texture.InitDesc {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget | TextureBuffer.BindFlag.IsUnorderedAccess,
				width = ksRenderer.TargetWidth,
				height = ksRenderer.TargetHeight,
				format = SlimDX.DXGI.Format.R16G16B16A16_Float,
			};
			uav_ = new Texture(uavDesc_);
			csMgr_.SetUAVs(new Texture[] { uav_ });
			var bufs = new IShaderView[] { gbuffers_[0], gbuffers_[1], gbuffers_[2], ksRenderer.DefaultDepthBuffer, lightMgr_.LightBuffer, shadowMap_.Map };
			csMgr_.SetResources(bufs);

			hdrResolveDesc_ = new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = ksRenderer.TargetWidth,
				height = ksRenderer.TargetHeight,
				format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
			};
			hdrResolveBuffer_ = new Texture(hdrResolveDesc_);

			forwardBuffer_ = new ksRenderer.FrameBuffer();
			forwardBuffer_.color_buffer_ = new Texture[] { uav_ };
			forwardBuffer_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;

			hdrResultBuffer_ = new ksRenderer.FrameBuffer();
			hdrResultBuffer_.color_buffer_ = new Texture[] { hdrResolveBuffer_ };
			hdrResultBuffer_.depth_stencil_ = null;

			// キューブマップ
			int cubeSize = 128;
			globalCapture_ = new ReflectionCapture(cubeSize, new Vector3(0, 3, 0));
			localCapture_ = new List<ReflectionCapture>();
			giTextures_ = new Texture[3];
			if (System.IO.File.Exists("asset/gitex_r.dds")) {
				giTextures_[0] = new Texture("asset/gitex_r.dds");
				giTextures_[1] = new Texture("asset/gitex_g.dds");
				giTextures_[2] = new Texture("asset/gitex_b.dds");
				csMgr_.SetResources(6, giTextures_[0]);
				csMgr_.SetResources(7, giTextures_[1]);
				csMgr_.SetResources(8, giTextures_[2]);
				cubeMapRenderEnable_ = false;
			}

			// グリッド上で生成
			if (cubeMapRenderEnable_) {
				Vector3 bounds = cubeMapInfo_.boundingMax - cubeMapInfo_.boundingMin;
				for (int z = 0; z < cubeMapInfo_.numZ; z++) {
					for (int y = 0; y < cubeMapInfo_.numY; y++) {
						for (int x = 0; x < cubeMapInfo_.numX; x++) {
							Vector3 add = new Vector3(bounds.X * ((float)x / (cubeMapInfo_.numX - 1)),
								bounds.Y * ((float)y / (cubeMapInfo_.numY - 1)),
								bounds.Z * ((float)z / (cubeMapInfo_.numZ - 1))
								);
							Vector3 pos = cubeMapInfo_.boundingMin + add;
							localCapture_.Add(new ReflectionCapture(cubeSize, pos));
						}
					}
				}
			}

			// アンチなし用プリム
			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			prim_ = new Prim("Direct", (uint)Shader.VertexAttr.TEXCOORD0);
			prim_.AddRect(ref rect);
			prim_.GetMaterial().SetShaderViewPS(0, gbuffers_[0]);
			prim_.GetMaterial().DepthState = RenderState.DepthState.None;

			// FXAA	
			fxaaPrim_ = new Prim("FXAA", (uint)Shader.VertexAttr.TEXCOORD0);
			fxaaPrim_.AddRect(ref rect);
			fxaaPrim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
			fxaaPrim_.GetMaterial().DepthState = RenderState.DepthState.None;

			// デバッグ表示用
			debugViewList_ = new Texture[] {
				hdrResolveBuffer_,
				gbuffers_[0],
				gbuffers_[1],
				gbuffers_[2],
				shadowMap_.Map,
				toneMap_.LuminanceAvgTexture,
			};
		}


		/// <summary>
		/// オブジェクト初期化
		/// </summary>
		void InitialzeSceneObject()
		{
			drawModel_ = new Model();
			drawModel_.Initialize("asset/sponza/sponza.obj", 2, true);

			// テクスチャの枚数で使用シェーダを切り替え
			foreach (var m in drawModel_.Materials) {
				int count = m.PsShaderViews.Count(c => c != null);
				if (count > 1) {
					// 法線マップ付
					m.SetShader("GBufferAccumulate");
				} else 
				{
					m.SetShader("GBufferAccumulate", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0));
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			prim_.Dispose();
			if (drawModel_ != null) drawModel_.Dispose();
			fxaaPrim_.Dispose();
			foreach (var t in gbuffers_) {
				t.Dispose();
			}
			uav_.Dispose();
			hdrResolveBuffer_.Dispose();

			globalCapture_.Dispose();
			foreach (var c in localCapture_) {
				c.Dispose();
			}
			if (giTextures_ != null) {
				foreach (var t in giTextures_) {
					t.Dispose();
				}
			}

			toneMap_.Dispose();
			shadowMap_.Dispose();
			lightMgr_.Dispose();
			csMgr_.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="w"></param>
		/// <param name="h"></param>
		public void ScreenSizeChanged(int w, int h)
		{
			csMgr_.ScreenSizeChanged(w, h);
			var bufs = new IShaderView[] { gbuffers_[0], gbuffers_[1], gbuffers_[2], ksRenderer.DefaultDepthBuffer, lightMgr_.LightBuffer, shadowMap_.Map };
			csMgr_.SetResources(bufs);

			// GBuffer
			for (int i = 0; i < gbufferDesc_.Length; i++ ) {
				gbufferDesc_[i].width = w;
				gbufferDesc_[i].height = h;
				gbuffers_[i].Dispose();
				gbuffers_[i].Initialize(gbufferDesc_[i]);
			}
			deferredBuffer_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;
			forwardBuffer_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;
			uav_.Dispose();
			uavDesc_.width = w;
			uavDesc_.height = h;
			uav_.Initialize(uavDesc_);
			hdrResolveBuffer_.Dispose();
			hdrResolveDesc_.width = w;
			hdrResolveDesc_.height = h;
			hdrResolveBuffer_.Initialize(hdrResolveDesc_);
			prim_.GetMaterial().SetShaderViewPS(0, debugViewList_[ViewModel.ViewIndex]);

			// カラーバッファ作り替え
			fxaaPrim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
		}


		/// <summary>
		/// 更新
		/// </summary>
		public void Update()
		{				
			// 入力
			Input.Update();

			// カメラ更新
			cameraCtrl_.Update();
		}


		/// <summary>
		/// 描画関連の更新
		/// </summary>
		public void UpdateDraw()
		{
			// レンダラ更新
			ksRenderer.Update();

			// ライト更新
			lightMgr_.Update();
			if (lightMgr_.LightCount != viewModel_.LightCount) {
				lightMgr_.LightCount = viewModel_.LightCount;
			}

			// 表示インデックスを更新
			prim_.GetMaterial().SetShaderViewPS(0, debugViewList_[ViewModel.ViewIndex]);
			csMgr_.CurrentMethod = (ComputeManager.Method)ViewModel.TiledRenderView;
		}


		/// <summary>
		/// 描画
		/// </summary>
		public void Draw()
		{
			UpdateDraw();

			ksRenderer.BeginDraw();
			{
				// シャドウマップ
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Create ShadowMap")) {
					shadowMap_.BeginRender();
					{
						ksRenderer.ClearDepthBuffer();
						SceneDraw();
					}
					shadowMap_.EndRender();
				}

				// キューブマップ
				if (cubeMapRenderEnable_ && !cubeMapRendered_) {
					RenderCubeMap();
					cubeMapRendered_ = true;
				}

				// ディファードパス
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Deferred Path")) {
					// GBuffer
					using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Render GBuffer")) {
						ksRenderer.CurrentDrawCamera = ksRenderer.Camera3D;
						//ksRenderer.BeginRender();
						RenderGBuffer();
						//ksRenderer.EndRender();
					}

					// ライティングCS
					using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Lighting")) {
						csMgr_.Bind();
						int x = Lib.ComputeShader.CalcThreadGroups(gbuffers_[0].Width, 32);
						int y = Lib.ComputeShader.CalcThreadGroups(gbuffers_[0].Height, 32);
						csMgr_.Dispatch(gbuffers_[0].Width, gbuffers_[0].Height);
						csMgr_.UnBind();
					}
				}

				// フォワードパス
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Forward Path")) {
					ksRenderer.BeginRender(forwardBuffer_);
					{
						// キューブマップデバッグ
						if (cubeMapRenderEnable_) {
							//globalCapture_.DebugDraw();
							//foreach (var c in localCapture_) {
							//	c.DebugDraw();
							//}
						}

						//// ライトのデバッグ描画
						//if (ViewModel.IsDrawLights
						//	&& ViewModel.ViewIndex == (int)ViewItems.Type.Result
						//	&& ViewModel.TiledRenderView != (int)TiledRenderItems.Type.LightCount
						//	) {
						//	lightMgr_.DebugDraw();
						//}
					}
					ksRenderer.EndRender();
				}

				// トーンマップ
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "HDR Resolve")) {
					if (viewModel_.IsEnableToneMap) {
						toneMap_.CalcLuminance(uav_);
						ksRenderer.BeginRender(hdrResultBuffer_);
						toneMap_.KeyValue= viewModel_.ToneMapKeyValue;
						toneMap_.ResolveHDR(uav_);
						ksRenderer.EndRender();
					} else {
						ksRenderer.BeginRender(hdrResultBuffer_);
						toneMap_.ResolveGamma(uav_);
						ksRenderer.EndRender();
					}
				}

				// 最終レンダリング
				ksRenderer.BeginRender();
				{
					// ライティング結果
					if (ViewModel.IsEnableFXAA && ViewModel.ViewIndex == (int)ViewItems.Type.Result) {
						using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "FXAA")) {
							fxaaPrim_.Draw();
						}
					} else {
						prim_.Draw();
					}
				}
				ksRenderer.EndRender();
			}
			ksRenderer.EndDraw();
		}


		/// <summary>
		/// シーン描画
		/// </summary>
		void SceneDraw()
		{
			if (drawModel_ != null) {
				drawModel_.Draw();
			}
		}


		/// <summary>
		/// GBufferへの描画
		/// </summary>
		void RenderGBuffer()
		{
			ksRenderer.BeginRender(deferredBuffer_);

			ksRenderer.ClearColorBuffer(new Color4(0.0f, 0.0f, 0.0f));
			ksRenderer.ClearDepthBuffer();

			SceneDraw();

			ksRenderer.EndRender();
		}


		/// <summary>
		/// キューブマップにレンダリング
		/// </summary>
		void RenderCubeMap()
		{
			// コンスタントバッファ更新定義
			bool isInit = false;
			Shader.SetConstantBufferUpdateFunc("CB_LightParam", (s, i) => {
				if (!isInit) {
					dynamic cb = i;
					cb.g_directionalLightDir = lightMgr_.DirectionalLightDir;
					cb.g_directionalLightIntensity = lightMgr_.DirectionalLightIntensity;
					cb.g_shadowMatrix = Matrix.Transpose(shadowMap_.Camera.ViewProjectionMatrix);
					isInit = true;
					return true;
				}
				return false;
			});

			// モデルのシェーダ差し換え
			Func<Shader, Shader> overrideFunc = (o) => {
				return ShaderManager.FindShader("ForwardRender", (uint)(Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0) & o.NeedVertexAttr);
			};

			// 描画アクション
			Action func = () => {
				ksRenderer.ClearColorBuffer(new Color4(1, 0, 0, 0));
				ksRenderer.ClearDepthBuffer();
				int shadowMapBindingPoint = 4;
				ksRenderer.SetShaderResourcePS(shadowMapBindingPoint, shadowMap_.Map);
				ksRenderer.SetSamplerStatePS(shadowMapBindingPoint, shadowMap_.Map.AddressingModeU, shadowMap_.Map.AddressingModeV);
				ShaderManager.UserShaderBindHandler += overrideFunc;
				SceneDraw();
				ShaderManager.UserShaderBindHandler -= overrideFunc;
			};

			//globalCapture_.Capture(func, 3);

			if (localCapture_.Count > 0) {
				foreach (var c in localCapture_) {
					c.Capture(func, 2);
				}

				int size = cubeMapInfo_.numZ * cubeMapInfo_.numY * cubeMapInfo_.numX * 4 * 2;
				DataStream data_r = new DataStream(size, true, true);
				DataStream data_g = new DataStream(size, true, true);
				DataStream data_b = new DataStream(size, true, true);
				for (int z = 0; z < cubeMapInfo_.numZ; z++) {
					for (int y = 0; y < cubeMapInfo_.numY; y++) {
						for (int x = 0; x < cubeMapInfo_.numX; x++) {
							int index = (z * cubeMapInfo_.numY * cubeMapInfo_.numX) + (y * cubeMapInfo_.numX) + x;
							var coef = localCapture_[index].SHCoef;
							for (int i = 0; i < 4; i++) {
								data_r.Write(Lib.Math.CPf32Tof16(coef[i].X));
								data_g.Write(Lib.Math.CPf32Tof16(coef[i].Y));
								data_b.Write(Lib.Math.CPf32Tof16(coef[i].Z));
								//data_r.Write(coef[i].X);
								//data_g.Write(coef[i].Y);
								//data_b.Write(coef[i].Z);
							}
						}
					}
				}

				var giTexDesc = new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = cubeMapInfo_.numX,
					height = cubeMapInfo_.numY,
					depth = cubeMapInfo_.numZ,
					format = SlimDX.DXGI.Format.R16G16B16A16_Float,
					//format = SlimDX.DXGI.Format.R32G32B32A32_Float,
				};
				giTexDesc.initStream = data_r;
				giTextures_[0] = new Texture(giTexDesc);
				giTextures_[0].SaveFile("asset/gitex_r.dds");
				giTexDesc.initStream = data_g;
				giTextures_[1] = new Texture(giTexDesc);
				giTextures_[1].SaveFile("asset/gitex_g.dds");
				giTexDesc.initStream = data_b;
				giTextures_[2] = new Texture(giTexDesc);
				giTextures_[2].SaveFile("asset/gitex_b.dds");
				csMgr_.SetResources(6, giTextures_[0]);
				csMgr_.SetResources(7, giTextures_[1]);
				csMgr_.SetResources(8, giTextures_[2]);
			}
		}


		/// <summary>
		/// モデルのロード
		/// </summary>
		/// <param name="name"></param>
		public void LoadModel(string name)
		{
			if (drawModel_ != null) {
				drawModel_.Dispose();
			}
			drawModel_.Initialize(name, 1, true);
			foreach (var m in drawModel_.Materials) {
				int count = m.PsShaderViews.Count(c => c != null);
				if (count > 1) {
					// 法線マップ付
					m.SetShader("GBufferAccumulate");
				} else {
					m.SetShader("GBufferAccumulate", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0));
				}
			}
		}
	}
}
