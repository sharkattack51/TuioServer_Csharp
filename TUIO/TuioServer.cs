using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

using OSC.NET;

namespace TUIO
{
    class TuioServer
    {
        private OSCTransmitter socket;
        private bool connected;

        private List<TuioCursor> cursorList;
        private List<TuioCursor> freeCursorList;
        private List<TuioCursor> freeCursorBuffer;

        private int maxCursorID;
        private int sessionID;
        private int currentFrame;
        private TuioTime currentFrameTime;
        private bool updateCursor;
        private long lastCursorUpdate;

        private static int MAX_UDP_SIZE = 65536;
        private static int MIN_UDP_SIZE = 576;

        public TuioServer(string host = "127.0.0.1", int port = 3333, int size = 65536)
        {
            if (size > MAX_UDP_SIZE) size = MAX_UDP_SIZE;
            if (size < MIN_UDP_SIZE) size = MIN_UDP_SIZE;

            try
            {
                this.socket = new OSCTransmitter(host, port);
                this.socket.Connect();

                this.connected = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.cursorList = new List<TuioCursor>();
            this.freeCursorList = new List<TuioCursor>();
            this.freeCursorBuffer = new List<TuioCursor>();

            this.currentFrameTime = TuioTime.SessionTime;
            this.currentFrame = -1;
            this.sessionID = -1;
            this.maxCursorID = -1;
            this.updateCursor = false;
            this.lastCursorUpdate = this.currentFrameTime.Seconds;

            Clear();
        }

        ~TuioServer()
        {
            Close();
        }

        public void Close()
        {
            if (this.connected && this.socket != null)
            {
                Clear();

                this.socket.Close();
                this.socket = null;

                this.connected = false;
            }
        }

        public void Clear()
        {
            if (this.connected && this.socket != null)
            {
                stopUntouchedMovingCursors();
                removeUntouchedStoppedCursors();
                sendEmptyCursorBundle();
            }
        }


        # region Tuio controll

        public TuioCursor addTuioCursor(float x, float y)
        {
            this.sessionID++;

            int cursorID = this.cursorList.Count();
            if ((this.cursorList.Count() <= this.maxCursorID) && (this.freeCursorList.Count > 0))
            {
                TuioCursor closestCursor = this.freeCursorList[0];
                foreach (TuioCursor f_cur in this.freeCursorList)
                {
                    if (f_cur.getDistance(x, y) < closestCursor.getDistance(x, y))
                        closestCursor = f_cur;
                }

                cursorID = closestCursor.CursorID;
                this.freeCursorList.Remove(closestCursor);
            }
            else
                this.maxCursorID = cursorID;

            TuioCursor tcur = new TuioCursor(this.currentFrameTime, this.sessionID, cursorID, x, y);
            this.cursorList.Add(tcur);
            this.updateCursor = true;

            return tcur;
        }

        public void updateTuioCursor(TuioCursor tcur, float x, float y)
        {
            if (tcur == null)
                return;

            if (tcur.TuioTime.Equals(this.currentFrameTime))
                return;

            tcur.update(this.currentFrameTime, x, y);
            this.updateCursor = true;
        }

        public void removeTuioCursor(TuioCursor tcur)
        {
            if (tcur == null)
                return;

            this.cursorList.Remove(tcur);
            tcur.remove(currentFrameTime);
            this.updateCursor = true;

            if(tcur.CursorID == this.maxCursorID)
            {
                this.maxCursorID = -1;

                if (this.cursorList.Count > 0)
                {
                    foreach(TuioCursor cur in this.cursorList)
                    {
                        if (cur.CursorID > this.maxCursorID)
                            this.maxCursorID = cur.CursorID;
                    }

                    this.freeCursorBuffer.Clear();
                    foreach(TuioCursor f_cur in this.freeCursorList)
                    {
                        if (f_cur.CursorID <= this.maxCursorID)
                            this.freeCursorBuffer.Add(f_cur);
                    }
                    this.freeCursorList = this.freeCursorBuffer;
                }
                else
                    this.freeCursorList.Clear();
            }
            else if (tcur.CursorID < this.maxCursorID)
                this.freeCursorList.Add(tcur);
        }

        # endregion


        public int getSessionID()
        {
            this.sessionID++;
            this.sessionID %= int.MaxValue;
            return this.sessionID;
        }

        public int getFrameID()
        {
            return this.currentFrame;
        }

        public TuioTime getFrameTime()
        {
            return this.currentFrameTime;
        }

        public void initFrame(TuioTime ttime)
        {
            this.currentFrameTime = ttime;
            this.currentFrame++;
            this.currentFrame %= int.MaxValue;
        }

        public void commitFrame()
        {
            OSCBundle bundle = null;

            if (this.updateCursor)
            {
                startCursorBundle(ref bundle);

                foreach (TuioCursor tcur in this.cursorList)
                {
                    if (tcur.TuioTime.Equals(this.currentFrameTime))
                        addCursorMessage(ref bundle, tcur);
                }

                sendCursorBundle(ref bundle, this.currentFrame);
            }
            else if (this.lastCursorUpdate < this.currentFrameTime.Seconds)
            {
                this.lastCursorUpdate = this.currentFrameTime.Seconds;

                startCursorBundle(ref bundle);
                sendCursorBundle(ref bundle, this.currentFrame);
            }

            this.updateCursor = false;
        }


        # region OSC Message

        private void sendEmptyCursorBundle()
        {
            OSCBundle bundle = new OSCBundle();

            OSCMessage msg1 = new OSCMessage("/tuio/2Dcur");
            msg1.Append("alive");

            bundle.Append(msg1);

            OSCMessage msg2 = new OSCMessage("/tuio/2Dcur");
            msg2.Append("fseq");
            msg2.Append(-1);

            bundle.Append(msg2);

            this.socket.Send(bundle);
        }

        private void startCursorBundle(ref OSCBundle bundle)
        {
            bundle = new OSCBundle();

            OSCMessage msg = new OSCMessage("/tuio/2Dcur");
            msg.Append("alive");
            foreach (TuioCursor tcur in this.cursorList)
                msg.Append(tcur.SessionID);

            bundle.Append(msg);
        }

        private void addCursorMessage(ref OSCBundle bundle, TuioCursor tcur)
        {
            OSCMessage msg = new OSCMessage("/tuio/2Dcur");
            msg.Append("set");
            msg.Append(tcur.SessionID);
            msg.Append(tcur.X);
            msg.Append(tcur.Y);
            msg.Append(tcur.XSpeed);
            msg.Append(tcur.YSpeed);
            msg.Append(tcur.MotionAccel);

            bundle.Append(msg);
        }

        private void sendCursorBundle(ref OSCBundle bundle, int fseq)
        {
            OSCMessage msg = new OSCMessage("/tuio/2Dcur");
            msg.Append("fseq");
            msg.Append(fseq);

            bundle.Append(msg);

            this.socket.Send(bundle);
        }

        # endregion


        # region get Corsor

        public TuioCursor getTuioCursor(long s_id)
        {
            foreach (TuioCursor tcur in this.cursorList)
            {
                if (tcur.SessionID == s_id)
                    return tcur;
            }

            return null;
        }

        public TuioCursor getClosestTuioCursor(float xp, float yp)
        {
            TuioCursor closestCursor = null;
            float closestDistance = 1.0f;

            foreach (TuioCursor tcur in this.cursorList)
            {
                float distance = tcur.getDistance(xp, yp);
                if (distance < closestDistance)
                {
                    closestCursor = tcur;
                    closestDistance = distance;
                }
            }

            return closestCursor;
        }

        public List<TuioCursor> getTuioCursors()
        {
            return this.cursorList;
        }

        public List<TuioCursor> getUntouchedCursors()
        {
            List<TuioCursor> untouched = new List<TuioCursor>();
            foreach (TuioCursor tcur in this.cursorList)
            {
                if (!tcur.TuioTime.Equals(this.currentFrameTime))
                    untouched.Add(tcur);
            }

            return untouched;
        }

        public void stopUntouchedMovingCursors()
        {
            foreach (TuioCursor tcur in this.cursorList)
            {
                if (!tcur.TuioTime.Equals(this.currentFrameTime) && tcur.isMoving)
                {
                    tcur.stop(this.currentFrameTime);
                    this.updateCursor = true;
                }
            }
        }

        public void removeUntouchedStoppedCursors()
        {
            for (int i = 0; i < this.cursorList.Count; i++)
            {
                TuioCursor tcur = this.cursorList[i];
                if (!tcur.TuioTime.Equals(this.currentFrameTime) && !tcur.isMoving)
                {
                    removeTuioCursor(tcur);
                    i = -1;
                }
            }
        }

        # endregion
    }
}
