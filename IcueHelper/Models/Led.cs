using Corsair.CUE.SDK;

namespace IcueHelper.Models
{
    /// <summary>
    /// class representing a single icue LED controlled by the SDK
    /// </summary>
    /// <seealso cref="Corsair.CUE.SDK.CorsairLedColor" />
    public class Led: CorsairLedColor
    {
        /// <summary>
        /// Gets the led position.
        /// </summary>
        /// <value>
        /// The led position.
        /// </value>
        public CorsairLedPosition LedPosition { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Led"/> class.
        /// </summary>
        /// <param name="ledPosition">The led position.</param>
        internal Led(CorsairLedPosition ledPosition) : base()
        {
            LedPosition = ledPosition;
            this.ledId = ledPosition.ledId;
            this.r = 0;
            this.g = 0;
            this.b = 0;
        }

    }
}
