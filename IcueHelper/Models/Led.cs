
using CUESDK;

namespace IcueHelper.Models
{
    /// <summary>
    /// class representing a single icue LED controlled by the SDK
    /// </summary>
    /// <seealso cref="Corsair.CUE.SDK.CorsairLedColor" />
    public class Led
    {

        //
        // Summary:
        //     Identifier of LED to set.
        public CorsairLedId LedId { get; set; }
        //
        // Summary:
        //     Red brightness [0..255].
        public int R { get; set; }
        //
        // Summary:
        //     Green brightness [0..255].
        public int G { get; set; }
        //
        // Summary:
        //     Blue brightness [0..255].
        public int B { get; set; }

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
            this.LedId = ledPosition.LedId;
            this.R = 0;
            this.G = 0;
            this.B = 0;
        }

        public CorsairLedColor CorsairLedColor
        {
            get
            {
                CorsairLedColor result = new CorsairLedColor();
                result.R = this.R;
                result.G = this.G;
                result.B = this.B;
                result.LedId = this.LedId;
                return result;
            }
        }

    }
}
