using System;

namespace RobotArmLibrary
{
    public class Arm
    {
		#region Constructors
		public Arm(int dof)
		{
			DoF = dof;
			Console.WriteLine($"New Arm Created with {DoF} DoF");
		}
		#endregion

		#region Properties
		private int _doF;
		public int DoF
		{
			get 
			{ 
				return _doF; 
			}
			set 
			{ 
				_doF = value; 
			}
		}

		#endregion

		#region Methods
		public byte[] CalcThrowArray(byte[] sensorData)
		{
			byte[] output = new byte[2000];
			return output;
		}
		#endregion
	}
}
