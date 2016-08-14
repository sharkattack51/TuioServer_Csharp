using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TUIO;

/**
* TuioServer_C# sample code.
* required TUIO11_NET libraries.
* http://prdownloads.sourceforge.net/reactivision/TUIO11_NET-1.1.5.zip?download
*/

class TuioSample
{
	private TuioServer server;

	
	public TuioSample()
	{
		this.server = new TuioServer();
	}

	~TuioSample()
	{
		this.server.Close();
	}


	// enterframe function
	public void ProcessTUIO(List<TrackingPoint> trackingPoints)
	{
		if(this.server == null)
			return;

		// init
		this.server.initFrame(TuioTime.SessionTime);

		// event process
		foreach(TrackingPoint pt in trackingPoints)
		{
			switch(pt.ptEvent)
			{
				case TrackingPoint.POINT_EVENT.DOWN:

					// cursor add
					TuioCursor tcur_new = this.server.addTuioCursor((float)pt.position.X, (float)pt.position.Y);
					break;

				case TrackingPoint.POINT_EVENT.MOVE:

					// cursor update
					foreach (TuioCursor tcur_move in this.server.getTuioCursors())
					{
						if (tcur_move.getDistance((float)pt.pre_position.X, (float)pt.pre_position.Y) == 0.0)
						{
							this.server.updateTuioCursor(tcur_move, (float)pt.position.X, (float)pt.position.Y);
							break;
						}
					}
					break;

				case TrackingPoint.POINT_EVENT.UP:

					// cursor remove
					foreach (TuioCursor tcur_del in this.server.getTuioCursors())
					{
						if (tcur_del.getDistance((float)pt.position.X, (float)pt.position.Y) == 0.0f)
						{
							this.server.removeTuioCursor(tcur_del);
							break;
						}
					}
					break;

				default: break;
			}
		}

		// commit
		this.server.stopUntouchedMovingCursors();
		this.server.removeUntouchedStoppedCursors();
		this.server.commitFrame();
	}

	public void ClearTUIO()
	{
		this.server.Clear();
	}

	public List<TuioCursor> GetCursors()
	{
		return this.server.getTuioCursors();
	}
}
