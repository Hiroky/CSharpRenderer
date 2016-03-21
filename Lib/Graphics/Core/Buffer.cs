using System;
using System.Runtime.InteropServices;
using System.IO;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

using D3D11Buffer = SlimDX.Direct3D11.Buffer;
using D3D11Resource = SlimDX.Direct3D11.Resource;


namespace Lib
{
	/// <summary>
	/// シェーダビュー
	/// </summary>
	public interface IShaderView
	{
		ShaderResourceView ShaderResourceView { get; }
		RenderTargetView RenderTargetView { get; }
		DepthStencilView DepthStencilView { get; }
		UnorderedAccessView UnorderedAccessView { get; }
	}


	/// <summary>
	/// 基底GPUバッファオブジェ
	/// </summary>
	public class Buffer : IDisposable
	{
		protected D3D11Resource buffer_ = null;
		protected ShaderResourceView shaderResourceView_;

		public ShaderResourceView ShaderResourceView { get { return shaderResourceView_; } }
		public D3D11Resource Resource { get { return buffer_; } }

		/// <summary>
		/// 終了処理
		/// </summary>
		public virtual void Dispose()
		{
			buffer_.Dispose();
			if (shaderResourceView_ != null) shaderResourceView_.Dispose();
		}
	}


	/// <summary>
	/// 頂点バッファ
	/// </summary>
	public class VertexBuffer : Buffer
	{
		public class BufferDesc
		{
			public DataStream data;
			public int data_size;
			public int stride;
		};

		protected int stride_;
		RenderState.PrimitiveTopology topology_ = RenderState.PrimitiveTopology.TriangleList;
		public RenderState.PrimitiveTopology Topology { get { return topology_; } set { topology_ = value; } }

		public VertexBuffer(BufferDesc desc)
		{
			buffer_ = new D3D11Buffer(GraphicsCore.D3dDevice, desc.data, desc.data_size, ResourceUsage.Default, BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
			stride_ = desc.stride;
		}

		public void Bind()
		{
			GraphicsCore.SetPrimitiveTopology(Topology);
			GraphicsCore.D3dImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding((D3D11Buffer)buffer_, stride_, 0));
		}
	}


	/// <summary>
	/// インデックスバッファ
	/// </summary>
	public class IndexBuffer : Buffer
	{
		public IndexBuffer(DataStream stream, int data_size)
		{
			buffer_ = new D3D11Buffer(GraphicsCore.D3dDevice, stream, data_size, ResourceUsage.Default, BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
		}

		public void Bind()
		{
			GraphicsCore.D3dImmediateContext.InputAssembler.SetIndexBuffer((D3D11Buffer)buffer_, Format.R32_UInt, 0);
		}
	}



	/// <summary>
	/// テクスチャバッファ
	/// </summary>
	public class TextureBuffer : Buffer
	{
		/// <summary>
		/// テクスチャデータヘッダ
		/// </summary>
		struct ksTexHeader
		{
			public ushort width;
			public ushort height;
			public ushort type;
			public ushort __padding;
			public int data_size;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct tgaHeader
		{
			public byte id;
			public byte colormap;
			public byte format;
			public short colormapOrigin;
			public short colormapLength;
			public byte colormapEntrySize;
			public short x;
			public short y;
			public short width;
			public short height;
			public byte depth;
			public byte flag;
		}

		public struct BufferDesc
		{
			public int width, height, depth;
			public Format format;
			public int mips;
			public BindFlag bindFlag;
			public byte[][][] initData;
			public DataStream initStream;
			public ResourceOptionFlags optionFlags;

			#region property
			public bool IsRenderTarget { get { return (bindFlag & BindFlag.IsRenderTarget) != 0; } }
			public bool IsDepthStencil { get { return (bindFlag & BindFlag.IsDepthStencil) != 0; } }
			public bool IsUnorderedAccess { get { return (bindFlag & BindFlag.IsUnorderedAccess) != 0; } }
			#endregion
		}

		public enum BindFlag
		{
			IsRenderTarget = 1 << 0,
			IsDepthStencil = 1 << 1,
			IsUnorderedAccess = 1 << 2,
		}

		int width_;
		int height_;
		int mips_;
		Format format_;
		RenderTargetView renderTargetView_;
		DepthStencilView depthStencilView_;
		UnorderedAccessView unorderedAccessView_;
		RenderTargetView[] arrayRenderTargetView_;

		public RenderTargetView RenderTargetView { get { return renderTargetView_; } }
		public DepthStencilView DepthStencilView { get { return depthStencilView_; } }
		public UnorderedAccessView UnorderedAccessView { get { return unorderedAccessView_; } }
		public RenderTargetView[] ArryaRenderTargetView { get { return arrayRenderTargetView_; } }

		public int Width { get { return width_; } }
		public int Height { get { return height_; } }
		public Format ImageFormat { get { return format_; } }


		/// <summary>
		/// ポインタから
		/// </summary>
		/// <param name="ptr"></param>
		public TextureBuffer(IntPtr ptr)
		{
			ksTexHeader header = (ksTexHeader)Marshal.PtrToStructure(ptr, typeof(ksTexHeader));
			int offset = Marshal.SizeOf(header);
			ptr += offset;

			// データをバイト配列にコピー
			byte[] buffer = new byte[header.data_size];
			Marshal.Copy(ptr, buffer, 0, header.data_size);

			// バッファから初期化
			shaderResourceView_ = ShaderResourceView.FromMemory(GraphicsCore.D3dDevice, buffer);
			buffer_ = shaderResourceView_.Resource;

			// 情報取得
			var tex = (Texture2D)shaderResourceView_.Resource;
			width_ = tex.Description.Width;
			height_ = tex.Description.Height;
			format_ = tex.Description.Format;
			mips_ = tex.Description.MipLevels;
		}

		/// <summary>
		/// ファイルから
		/// </summary>
		/// <param name="file_name"></param>
		public TextureBuffer(string file_name)
		{
			using (var stream = new System.IO.FileStream(file_name, System.IO.FileMode.Open, FileAccess.Read)) {
				byte[] data = new byte[stream.Length];
				stream.Read(data, 0, (int)stream.Length);
				MemoryStream s = new MemoryStream(data);

				// 名前からDDSかを判別
				string ext = Path.GetExtension(file_name).ToLower();
				if (ext == ".kstex") {
					// ksTex
					// byte配列からポインタを取得
					var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
					IntPtr ptr = handle.AddrOfPinnedObject();
					ksTexHeader header = (ksTexHeader)Marshal.PtrToStructure(ptr, typeof(ksTexHeader));
					int offset = Marshal.SizeOf(header);

					// ストリームの一をセットしてSRV生成
					s.Position = offset;
					shaderResourceView_ = ShaderResourceView.FromStream(GraphicsCore.D3dDevice, s, header.data_size);
					handle.Free();
				} else if( ext == ".tga" ) {
					// tga
					var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
					IntPtr ptr = handle.AddrOfPinnedObject();
					tgaHeader header = (tgaHeader)Marshal.PtrToStructure(ptr, typeof(tgaHeader));
					int offset = Marshal.SizeOf(header);
					int image_size = header.width * header.height * 4;	// 32bit固定
					byte[] image_buf = new byte[image_size];
					// 上下反転
					int w = header.width;
					int h = header.height;
					int dst_row_size = w * 4;
					int bpp = header.depth / 8;
					int src_row_size = w * bpp;
					Func<int, int, int> getAddrFunc;
					if ((header.flag & (1 << 5)) == 0) {
						getAddrFunc = (height, current_heght) => {
							return ((height - 1 - current_heght) * dst_row_size);
						};
					} else {
						getAddrFunc = (height, current_heght) => {
							return (current_heght * dst_row_size);
						};
					}
					for (int ch = 0; ch < h; ch++) {
						int src_start = src_row_size * ch + offset;
						int dst_start = getAddrFunc(h, ch);
						for (int cw = 0; cw < w; cw++) {
							int src = src_start + cw * bpp;
							int dst = dst_start + cw * 4;
							image_buf[dst + 0] = data[src + 0];
							image_buf[dst + 1] = data[src + 1];
							image_buf[dst + 2] = data[src + 2];
							image_buf[dst + 3] = (bpp == 4) ? data[src + 3] : (byte)255;
						}
					}
					byte[][][] data_ary = new byte[][][] {
						new byte[][] {
							image_buf,
						},
					};
					BufferDesc desc = new BufferDesc() {
						width = header.width,
						height = header.height,
						format = Format.B8G8R8A8_UNorm,
						initData = data_ary,
						mips = 0,
					};
					Initialize(desc);
					handle.Free();
				} else {
					// その他フォーマット
					shaderResourceView_ = ShaderResourceView.FromStream(GraphicsCore.D3dDevice, s, (int)stream.Length);
				}

				// 情報取得
				if (shaderResourceView_.Resource is Texture2D) {
					var tex = (Texture2D)shaderResourceView_.Resource;
					width_ = tex.Description.Width;
					height_ = tex.Description.Height;
					format_ = tex.Description.Format;
					mips_ = tex.Description.MipLevels;
				} else if (shaderResourceView_.Resource is Texture3D) {
					var tex = (Texture3D)shaderResourceView_.Resource;
					width_ = tex.Description.Width;
					height_ = tex.Description.Height;
					format_ = tex.Description.Format;
					mips_ = tex.Description.MipLevels;
				}

				buffer_ = shaderResourceView_.Resource;
			}
		}


		/// <summary>
		/// descから
		/// </summary>
		/// <param name="desc"></param>
		public TextureBuffer(BufferDesc desc)
		{
			if (desc.depth > 0) {
				Initialize3D(desc);
			} else {
				Initialize(desc);
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="desc"></param>
		public void Initialize(BufferDesc desc)
		{
			var device = GraphicsCore.D3dDevice;

			// 情報取得
			width_ = desc.width;
			height_ = desc.height;
			format_ = desc.format;
			mips_ = desc.mips;
			if (mips_ == 0) {
				mips_ = 1;
			}

			BindFlags bindFlags = BindFlags.ShaderResource;
			if (desc.IsRenderTarget) bindFlags |= BindFlags.RenderTarget;
			if (desc.IsDepthStencil) bindFlags |= BindFlags.DepthStencil;
			if (desc.IsUnorderedAccess) bindFlags |= BindFlags.UnorderedAccess;

			CpuAccessFlags acc_flag = CpuAccessFlags.None;
			ResourceUsage res_usage = ResourceUsage.Default;
			bool isCube = (desc.optionFlags & ResourceOptionFlags.TextureCube) != 0;
			int array_size = (!isCube) ? 1 : 6;

			// 初期化データ
			DataRectangle[] dataRect = null;
			if (desc.initData != null) {
				dataRect = new DataRectangle[desc.initData.Length * desc.initData[0].Length];
				int index = 0;
				for (int i = 0; i < desc.initData[0].Length; i++) {
					for (int m = 0; m < desc.initData.Length; m++) {
						dataRect[index++] = new DataRectangle((width_ >> m) * 4, new DataStream(desc.initData[m][i], true, true));
					}
				}
			}

			// テクスチャオブジェクト
			Texture2DDescription textureDescription = new Texture2DDescription {
				ArraySize = array_size,
				BindFlags = bindFlags,
				CpuAccessFlags = acc_flag,
				Format = desc.format,
				Height = desc.height,
				Width = desc.width,
				MipLevels = mips_,
				//OptionFlags = (mips_ > 1 && needsGpuWrite) ? ResourceOptionFlags.GenerateMipMaps : ResourceOptionFlags.None,
				OptionFlags = desc.optionFlags,
				SampleDescription = new SampleDescription(1, 0),
				Usage = res_usage
			};
			var texture2d = new Texture2D(device, textureDescription, dataRect);
			buffer_ = texture2d;

			// 初期データ解放
			if (dataRect != null) {
				foreach (var d in dataRect) {
					d.Data.Close();
				}
			}

			Format srvFormat = desc.format;

			// Special case for depth
			if (desc.IsDepthStencil) {
				srvFormat = (desc.format == Format.R32_Typeless) ? Format.R32_Float : Format.R24_UNorm_X8_Typeless;
			}

			ShaderResourceViewDescription srvViewDesc = new ShaderResourceViewDescription {
				ArraySize = 0,
				Format = srvFormat,
				Dimension = (isCube) ? ShaderResourceViewDimension.TextureCube : ShaderResourceViewDimension.Texture2D,
				Flags = 0,
				FirstArraySlice = 0,
				MostDetailedMip = 0,
				MipLevels = mips_
			};
			shaderResourceView_ = new ShaderResourceView(device, texture2d, srvViewDesc);

			// レンダーターゲットビュー
			if (desc.IsRenderTarget) {
				if (!isCube) {
					// 通常
					RenderTargetViewDescription rtViewDesc = new RenderTargetViewDescription {
						Dimension = RenderTargetViewDimension.Texture2D,
						Format = desc.format,
						MipSlice = 0,
					};
					renderTargetView_ = new RenderTargetView(device, texture2d, rtViewDesc);
				} else {
					// キューブマップ
					RenderTargetViewDescription rtViewDesc = new RenderTargetViewDescription {
						Dimension = RenderTargetViewDimension.Texture2DArray,
						Format = desc.format,
						MipSlice = 0,
						ArraySize = 1,
					};
					arrayRenderTargetView_ = new RenderTargetView[array_size * mips_];
					for (int m = 0; m < mips_; m++) {
						for (int i = 0; i < array_size; i++) {
							rtViewDesc.MipSlice = m;
							rtViewDesc.FirstArraySlice = i;
							arrayRenderTargetView_[m * 6 + i] = new RenderTargetView(device, texture2d, rtViewDesc);
						}
					}
				}
			}

			// デプスステンシルビュー
			if (desc.IsDepthStencil) {
				DepthStencilViewDescription dsViewDesc = new DepthStencilViewDescription {
					ArraySize = 0,
					Format = (desc.format == Format.R32_Typeless) ? Format.D32_Float : Format.D24_UNorm_S8_UInt,
					Dimension = DepthStencilViewDimension.Texture2D,
					MipSlice = 0,
					Flags = 0,
					FirstArraySlice = 0
				};

				depthStencilView_ = new DepthStencilView(device, texture2d, dsViewDesc);
			}

			// UAV
			if (desc.IsUnorderedAccess) {
				UnorderedAccessViewDescription uavDesc = new UnorderedAccessViewDescription {
					ArraySize = 0,
					DepthSliceCount = 1,
					Dimension = UnorderedAccessViewDimension.Texture2D,
					ElementCount = 1,
					FirstArraySlice = 0,
					FirstDepthSlice = 0,
					FirstElement = 0,
					Flags = UnorderedAccessViewBufferFlags.None,
					Format = desc.format,
					MipSlice = 0
				};
				unorderedAccessView_ = new UnorderedAccessView(device, texture2d, uavDesc);
			}
		}


		/// <summary>
		/// 3Dテクスチャの初期化
		/// </summary>
		/// <param name="desc"></param>
		public void Initialize3D(BufferDesc desc)
		{
			var device = GraphicsCore.D3dDevice;

			// 情報取得
			width_ = desc.width;
			height_ = desc.height;
			format_ = desc.format;
			mips_ = desc.mips;
			if (mips_ == 0) {
				mips_ = 1;
			}

			BindFlags bindFlags = BindFlags.ShaderResource;
			if (desc.IsRenderTarget) bindFlags |= BindFlags.RenderTarget;
			if (desc.IsUnorderedAccess) bindFlags |= BindFlags.UnorderedAccess;
			CpuAccessFlags acc_flag = CpuAccessFlags.None;
			ResourceUsage res_usage = ResourceUsage.Default;

			// 初期化データ
			DataBox databox = null;
			if (desc.initData != null) {
				int size = desc.initData.Length * desc.initData[0].Length * desc.initData[0][0].Length;
				DataStream s = new DataStream(size, true, true);
				foreach (var z in desc.initData) {
					foreach (var y in z) {
						s.Write(y, 0, y.Length);
					}
				}
				databox = new DataBox(desc.width, desc.width * desc.height, s);
			}
			if (desc.initStream != null) {
				int bpp = (int)desc.initStream.Length / (desc.width * desc.height * desc.depth);
				desc.initStream.Position = 0;
				databox = new DataBox(desc.width * bpp, desc.width * desc.height * bpp, desc.initStream);
			}

			// テクスチャオブジェクト
			Texture3DDescription textureDescription = new Texture3DDescription {
				BindFlags = bindFlags,
				CpuAccessFlags = acc_flag,
				Format = desc.format,
				Height = desc.height,
				Width = desc.width,
				Depth = desc.depth,
				MipLevels = mips_,
				OptionFlags = desc.optionFlags,
				Usage = res_usage
			};
			var texture3d = new Texture3D(device, textureDescription, databox);
			buffer_ = texture3d;

			// 初期データ解放
			if (databox != null) {
				databox.Data.Close();
			}

			Format srvFormat = desc.format;
			ShaderResourceViewDescription srvViewDesc = new ShaderResourceViewDescription {
				ArraySize = 0,
				Format = srvFormat,
				Dimension = ShaderResourceViewDimension.Texture3D,
				Flags = 0,
				FirstArraySlice = 0,
				MostDetailedMip = 0,
				MipLevels = mips_
			};
			shaderResourceView_ = new ShaderResourceView(device, texture3d, srvViewDesc);

			// レンダーターゲットビュー
			if (desc.IsRenderTarget) {
				renderTargetView_ = new RenderTargetView(device, texture3d);
			}
			// UAV
			if (desc.IsUnorderedAccess) {
				unorderedAccessView_ = new UnorderedAccessView(device, texture3d);
			}
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public override void Dispose()
		{
			base.Dispose();
			if (renderTargetView_ != null) renderTargetView_.Dispose();
			if (depthStencilView_ != null) depthStencilView_.Dispose();
			if (unorderedAccessView_ != null) unorderedAccessView_.Dispose();
			if (arrayRenderTargetView_ != null) {
				foreach (var t in arrayRenderTargetView_) {
					t.Dispose();
				}
			}
		}


		/// <summary>
		/// テクスチャデータをセットする
		/// </summary>
		/// <param name="slot"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public void SetBufferData(int slot, int level, byte[] data)
		{
			DataBox box = GraphicsCore.D3dImmediateContext.MapSubresource(buffer_ as Texture2D, level, slot, MapMode.Write, SlimDX.Direct3D11.MapFlags.None);
			if (box.Data.Length != data.Length) {
				GraphicsCore.D3dImmediateContext.UnmapSubresource(buffer_ as Texture2D, slot);
				new Exception();
				return;
			}
			box.Data.Write(data, 0, (int)box.Data.Length);
			GraphicsCore.D3dImmediateContext.UnmapSubresource(buffer_ as Texture2D, slot);
		}


		/// <summary>
		/// VRAMのデータを取得する
		/// </summary>
		public byte[] GetBufferData(int slot = 0)
		{
			var device = GraphicsCore.D3dDevice;

			// テンポラリリソース生成
			Texture2DDescription desc = new Texture2DDescription {
				Width = width_,
				Height = height_,
				MipLevels = mips_,
				ArraySize = 6,
				Format = format_,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Staging,
				CpuAccessFlags = CpuAccessFlags.Read,
			};
			Texture2D texture = new Texture2D(device, desc);

			// コピー(SlimDXでは何事もなく失敗することがあるのでうまくいかないときはここを疑う)
			GraphicsCore.D3dImmediateContext.CopyResource(buffer_, texture);

			// マップしてデータ取得
			DataBox box = GraphicsCore.D3dImmediateContext.MapSubresource(texture, 0, slot, MapMode.Read, SlimDX.Direct3D11.MapFlags.None);
			byte[] ary = new byte[box.Data.Length];
			box.Data.Read(ary, 0, (int)box.Data.Length);
			GraphicsCore.D3dImmediateContext.UnmapSubresource(texture, slot);
			texture.Dispose();

			return ary;
		}

		public DataStream GetBufferDataStream(int slot = 0)
		{
			byte[] ary = GetBufferData(slot);
			DataStream stream = new DataStream(ary, true, true);
			return stream;
		}
	}


	/// <summary>
	/// 構造化バッファ(IShaderViewを継承させるのは別の方法を要検討)
	/// </summary>
	public class StructuredBuffer : Buffer, IShaderView
	{
		public struct BufferDesc
		{
			public int size;
			public int stride;
			public bool bindUAV;
			public byte[] initData;
		}

		int stride_;
		UnorderedAccessView unorderedAccessView_;

		public int Stride { get { return stride_; } }
		public UnorderedAccessView UnorderedAccessView { get { return unorderedAccessView_; } }
		public RenderTargetView RenderTargetView { get { return null; } }
		public DepthStencilView DepthStencilView { get { return null; } }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="desc"></param>
		public StructuredBuffer(BufferDesc desc)
		{
			Initialize(desc);
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public override void Dispose()
		{
			base.Dispose();
			if (unorderedAccessView_ != null) unorderedAccessView_.Dispose();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="desc"></param>
		public void Initialize(BufferDesc desc)
		{
			var device = GraphicsCore.D3dDevice;
			stride_ = desc.stride;

			BindFlags flags = BindFlags.ShaderResource;
			if (desc.bindUAV) flags |= BindFlags.UnorderedAccess;

			BufferDescription bufDesc = new BufferDescription {
				SizeInBytes = desc.size,
				StructureByteStride = desc.stride,
				BindFlags = flags,
				OptionFlags = ResourceOptionFlags.StructuredBuffer,
				Usage = ResourceUsage.Default,
				CpuAccessFlags = CpuAccessFlags.None,
			};

			// 初期化データ
			if (desc.initData != null) {
				DataStream stream = new DataStream(desc.initData, true, false);
				buffer_ = new D3D11Buffer(device, stream, bufDesc);
				stream.Close();
			} else {
				buffer_ = new D3D11Buffer(device, bufDesc);
			}

			// SRV
			ShaderResourceViewDescription srvViewDesc = new ShaderResourceViewDescription {
				Format = Format.Unknown,
				Dimension = ShaderResourceViewDimension.Buffer,
				ElementWidth = desc.size / desc.stride,
			};
			shaderResourceView_ = new ShaderResourceView(device, buffer_, srvViewDesc);

			if (desc.bindUAV) {
				UnorderedAccessViewDescription uavDesc = new UnorderedAccessViewDescription {
					Dimension = UnorderedAccessViewDimension.Buffer,
					ElementCount = desc.size / desc.stride,
					Flags = UnorderedAccessViewBufferFlags.None,
					Format = Format.Unknown,
				};
				unorderedAccessView_ = new UnorderedAccessView(device, buffer_, uavDesc);
			}
		}


		public void SetBufferData(DataStream stream)
		{
			var context = GraphicsCore.D3dImmediateContext;
			DataBox box = new DataBox(0, 0, stream);
			context.UpdateSubresource(box, buffer_, 0);
			stream.Close();
		}


		public void SetBufferData(byte[] data)
		{
			var context = GraphicsCore.D3dImmediateContext;
			DataStream stream = new DataStream(data, true, false);
			DataBox box = new DataBox(0, 0, stream);
			context.UpdateSubresource(box, buffer_, 0);
			stream.Close();
		}


		public void SetBufferData(IntPtr data, int size)
		{
			var context = GraphicsCore.D3dImmediateContext;
			DataStream stream =  new DataStream(data, size, true, false);
			DataBox box = new DataBox(0, 0, stream);
			context.UpdateSubresource(box, buffer_, 0);
			stream.Close();
		}
	}
}
