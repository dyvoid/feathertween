namespace PATween.Internal
{
	internal class TweenData
	{
		private TweenStatus status;

		public TweenStatus Status
		{
			get => status;
			set => status = value;
		}

		public virtual void Reset()
		{
			status = TweenStatus.Disposed;
		}
	}
}
