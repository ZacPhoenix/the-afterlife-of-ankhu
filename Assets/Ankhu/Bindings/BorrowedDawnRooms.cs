using System;
using System.Collections.Generic;
using Ankhu.BorrowedDawn;

namespace Ankhu.Bindings
{
    /// <summary>
    /// The playable room sequence: Anpu's three teaching rooms, then Borrowed Dawn. Maps each rules
    /// room id to the Unity scene that plays it, so a solved room knows which scene comes next.
    /// </summary>
    public static class BorrowedDawnRooms
    {
        public const string BorrowedDawnId = "borrowed-dawn";

        private static readonly Dictionary<string, string> SceneNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { TeachingRooms.Cistern.Id, "Teach1_Cistern" },
            { TeachingRooms.HeldStep.Id, "Teach2_HeldStep" },
            { TeachingRooms.LingeringShadow.Id, "Teach3_LingeringShadow" },
            { BorrowedDawnId, "BorrowedDawn_Greybox" },
        };

        /// <summary>Room ids in play order.</summary>
        public static IReadOnlyList<string> SequenceIds
        {
            get
            {
                var ids = new List<string>(TeachingRooms.Sequence.Count);
                foreach (RoomDefinition room in TeachingRooms.Sequence)
                {
                    ids.Add(room.Id);
                }
                return ids;
            }
        }

        /// <summary>The room for an id. Null or empty means Borrowed Dawn. Unknown ids throw.</summary>
        public static RoomDefinition Resolve(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) || roomId == BorrowedDawnId)
            {
                return RoomDefinition.BorrowedDawn(RuleProfile.Greybox);
            }
            foreach (RoomDefinition room in TeachingRooms.Sequence)
            {
                if (room.Id == roomId)
                {
                    return room;
                }
            }
            throw new ArgumentException("No Borrowed Dawn room has the id '" + roomId + "'.", nameof(roomId));
        }

        public static bool IsTeachingRoom(string roomId)
        {
            return !string.IsNullOrEmpty(roomId) && roomId != BorrowedDawnId;
        }

        /// <summary>Scene name for a room id, or null when the id has no scene.</summary>
        public static string SceneNameFor(string roomId)
        {
            return SceneNames.TryGetValue(string.IsNullOrEmpty(roomId) ? BorrowedDawnId : roomId, out string name) ? name : null;
        }

        /// <summary>Scene name of the room after this one, or null for the last room.</summary>
        public static string NextSceneName(string roomId)
        {
            IReadOnlyList<RoomDefinition> sequence = TeachingRooms.Sequence;
            string id = string.IsNullOrEmpty(roomId) ? BorrowedDawnId : roomId;
            for (int i = 0; i < sequence.Count - 1; i++)
            {
                if (sequence[i].Id == id)
                {
                    return SceneNameFor(sequence[i + 1].Id);
                }
            }
            return null;
        }
    }
}
