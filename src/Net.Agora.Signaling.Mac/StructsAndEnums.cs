namespace Net.Agora.Signaling.Mac {

	// AgoraRtmLinkState — AgoraRtmEnumerates.h. The state half of AgoraRtmLinkStateEvent, which
	// RTM 2.3.0 left as the only way to observe the client's link: it deleted
	// AgoraRtmClientConnectionState (and its change-reason companion) along with the callback that
	// carried them. Note the values do not line up with the old enum — Idle is 0 and there is no
	// Reconnecting; a transient drop shows as Suspended, and Disconnected means the link is down
	// for good until the client acts.
	public enum AgoraRtmLinkState : long {
		Idle = 0,
		Connecting = 1,
		Connected = 2,
		Disconnected = 3,
		Suspended = 4,
		Failed = 5,
	}

	// AgoraRtmChannelType — AgoraRtmEnumerates.h.
	public enum AgoraRtmChannelType : long {
		None = 0,
		Message = 1,
		Stream = 2,
		User = 3,
	}
}
