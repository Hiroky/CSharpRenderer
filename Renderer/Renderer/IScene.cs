using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Renderer
{
	interface IScene : IDisposable
	{
		MainWindowViewModel ViewModel { get; }
		void ScreenSizeChanged(int w, int h);
		void Update();
		void Draw();
	}
}
