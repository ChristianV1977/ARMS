﻿
namespace Rynchodon.AntennaRelay
{
	/// <summary>
	/// Participant in a relay network.
	/// </summary>
	public interface IRelayPart
	{

		RelayStorage GetStorage();

	}
}