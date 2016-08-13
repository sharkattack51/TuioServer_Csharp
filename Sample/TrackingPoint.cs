using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using TUIO;

class TrackingPoint
{
	public enum POINT_EVENT
	{
		DOWN = 0,
		MOVE,
		UP
	}
	
	public POINT_EVENT ptEvent = POINT_EVENT.DOWN;
	public bool isTracked = false;
	public Point position = new Point();
	public Point pre_position = new Point();

	public UrgPoint()
	{
	
	}
}