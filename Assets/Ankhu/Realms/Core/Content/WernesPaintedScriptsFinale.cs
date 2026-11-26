using System.Collections.Generic;

namespace Ankhu.Realms.Core.Content
{
    /// <summary>
    /// Zac 2026-11-25: the finale of the first realm. With all four Wernes rooms done the hub door opens on the
    /// terrace at dusk, and Ankhu and Anpu watch Ra's night barque pass down the valley river (the first hour of the
    /// Amduat): a colossal sun disc under Mehen's arch, while the shadow of Apep slides under the water and its eye
    /// opens. A frozen painted cinematic with no spell words, so it adds no glyphs. Then on to Rosetau.
    /// Cues: "barque_rise" starts the barque's glide and the light, "apep_pass" the shadow, "apep_eye" the eye.
    /// </summary>
    public static partial class WernesPaintedScripts
    {
        public const string Finale = "Wernes_Finale";

        private static SceneScript BuildFinale()
        {
            var L = new Lines("wernes_finale");
            var room = new RoomDef
            {
                Id = "wernes_finale",
                Title = "The Barque of Ra",
                Verbs = new HashSet<string>(),
            };
            room.Objects.Add(Obj("ankhu", "Ankhu", "terrace", null, null));

            var s = new SceneScript { SceneName = Finale, Realm = "Wernes", Title = "The Barque of Ra", Room = room };

            s.Beats.Add(new Beat("barque", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Wait(1.5f),
                Step.Caption("The terrace again. The fields have gone dark. Far down the river, the horizon is burning."),
                L.S(Speaker.Ankhu, "The sun went down hours ago."),
                L.S(Speaker.Anpu, "It went down. Now it comes through here."),
                Step.Cue("barque_rise"),
                Step.Wait(4.0f),
                Step.Title("THE BARQUE OF RA", 5f),
                Step.Caption("A boat the length of the valley comes down the river. On its deck rides the sun, and around the sun the serpent Mehen holds its coil."),
                L.S(Speaker.Ankhu, "Nobody is rowing it."),
                L.S(Speaker.Anpu, "Nobody needs to. Every night it crosses the twelve hours of the dark, and every morning it comes out the other side. That is the only reason there is a morning."),
                Step.Wait(2.0f),
                Step.Cue("apep_pass"),
                Step.Wait(3.0f),
                Step.Caption("Under the water something longer than the boat is moving the other way."),
                L.S(Speaker.Ankhu, "Anpu. In the river."),
                L.S(Speaker.Anpu, "Do not look at it."),
                Step.Cue("apep_eye"),
                Step.Wait(2.5f),
                L.S(Speaker.Ankhu, "It opened its eye."),
                L.S(Speaker.Anpu, "Apep. It waits under every hour of the night for the boat to fail. It never has. Not yet."),
                Step.Wait(2.0f),
                L.S(Speaker.Ankhu, "And if it does?"),
                L.S(Speaker.Anpu, "Then no one wakes. Not the living, not the dead. The light is going down to Rosetau. So are we."),
                Step.Wait(2.0f),
                Step.Caption("The barque passes into the hills, and the dark comes back behind it."),
                Step.Wait(2.0f),
                Step.Next()));
            return s;
        }
    }
}
