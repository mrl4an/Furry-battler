using System;

namespace AnimalBattler.Client.Lobby
{
    [Serializable] public sealed class RoomPlayer
    {
        public string playerId;
        public string nickname;
        public bool isBot;
        public bool ready;
    }

    [Serializable] public sealed class CardDefinitionMessage
    {
        public string id;
        public string group;
        public string rarity;
        public int attack;
        public int health;
        public CardAbilityMessage ability;
        public int copies;
    }

    [Serializable] public sealed class CardAbilityMessage
    {
        public string kind;
        public int amount;
    }

    [Serializable] public sealed class CardInstanceMessage
    {
        public string id;
        public string cardId;
        public int copies;
        public int tier;
    }

    [Serializable] public sealed class ShopOfferMessage
    {
        public string cardId;
        public bool frozen;
    }

    [Serializable] public sealed class MatchSelfMessage
    {
        public string playerId;
        public int health;
        public int coins;
        public int availableCoins;
        public bool ready;
        public CardInstanceMessage[] hand;
        public CardInstanceMessage[] board;
        public ShopOfferMessage[] shop;
        public int[] bids;
    }

    [Serializable] public sealed class MatchOpponentMessage
    {
        public string playerId;
        public int health;
        public bool ready;
        public int handCount;
        public CardInstanceMessage[] board;
    }

    [Serializable] public sealed class AuctionResultMessage
    {
        public int slot;
        public string cardId;
        public string winnerId;
        public int firstBid;
        public int secondBid;
    }

    [Serializable] public sealed class BattleCardMessage
    {
        public int playerIndex;
        public int slot;
        public string id;
        public string cardId;
        public int copies;
        public int attack;
        public int health;
    }

    [Serializable] public sealed class BattleEventMessage
    {
        public int turn;
        public string attackerId;
        public string targetId;
        public int attackDamage;
        public int counterDamage;
        public int extraDamageToAttacker;
        public int extraDamageToTarget;
        public int poisonToAttacker;
        public int poisonToTarget;
        public bool attackerShieldUsed;
        public bool targetShieldUsed;
        public int attackerHealthAfter;
        public int targetHealthAfter;
        public string[] deadCardIds;
    }

    [Serializable] public sealed class BattleMessage
    {
        public int winnerIndex;
        public BattleCardMessage[] openingCards;
        public BattleEventMessage[] events;
    }

    [Serializable] public sealed class ServerMessage
    {
        public string type;
        public string code;
        public string roomCode;
        public long revision;
        public string playerId;
        public int playerCount;
        public RoomPlayer[] players;
        public CardDefinitionMessage[] cards;
        public string phase;
        public string backgroundId;
        public int round;
        public string endsAtUtc;
        public MatchSelfMessage self;
        public MatchOpponentMessage opponent;
        public string[] auction;
        public AuctionResultMessage[] auctionResults;
        public BattleMessage battle;
        public string winnerId;
        public bool isDraw;
        public bool rematchReady;
    }

    [Serializable] public sealed class GameRequest
    {
        public string type;
        public string nickname;
        public string cardId;
        public int slot;
        public int sourceSlot;
        public int shopSlot;
        public int targetSlot;
        public int amount;
        public bool ready;
        public bool frozen;
    }
}
