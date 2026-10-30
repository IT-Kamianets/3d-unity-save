namespace ITKamianets.Engine.Save
{
	/// <summary>What happened when a save was read.</summary>
	public enum SaveStatus
	{
		/// <summary>The save was read; <see cref="SaveResult{T}.Value"/> holds it.</summary>
		Ok,
		/// <summary>There is no save in this slot yet: start a new one.</summary>
		NotFound,
		/// <summary>The file could not be parsed. It was kept next to the slot as <c>.corrupt-*.json</c>.</summary>
		Corrupt,
		/// <summary>The save was written by a newer game version. It is left untouched.</summary>
		UnsupportedVersion,
		/// <summary>The file could not be read or written (permissions, disk).</summary>
		IoError,
	}

	/// <summary>A save read: always check <see cref="Status"/>; <see cref="Value"/> is only set for <see cref="SaveStatus.Ok"/>.</summary>
	public readonly struct SaveResult<T>
	{
		public readonly SaveStatus Status;
		public readonly T Value;
		public readonly string Message;

		public SaveResult(SaveStatus status, T value, string message = null)
		{
			Status = status;
			Value = value;
			Message = message;
		}

		public bool Ok => Status == SaveStatus.Ok;
	}
}
