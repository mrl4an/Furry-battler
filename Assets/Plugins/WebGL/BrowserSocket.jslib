mergeInto(LibraryManager.library, {
  AnimalBattlerSocketConnect: function (urlPtr, objectPtr) {
    var url = UTF8ToString(urlPtr);
    var objectName = UTF8ToString(objectPtr);
    if (Module.animalBattlerSocket) Module.animalBattlerSocket.close();
    var socket = new WebSocket(url);
    Module.animalBattlerSocket = socket;
    socket.onopen = function () { SendMessage(objectName, "OnSocketOpened"); };
    socket.onmessage = function (event) { SendMessage(objectName, "OnSocketMessage", event.data); };
    socket.onerror = function () { SendMessage(objectName, "OnSocketError", "connectionFailed"); };
    socket.onclose = function () {
      if (Module.animalBattlerSocket === socket) SendMessage(objectName, "OnSocketClosed");
    };
  },
  AnimalBattlerSocketSend: function (messagePtr) {
    if (Module.animalBattlerSocket && Module.animalBattlerSocket.readyState === WebSocket.OPEN)
      Module.animalBattlerSocket.send(UTF8ToString(messagePtr));
  },
  AnimalBattlerSocketClose: function () {
    if (Module.animalBattlerSocket) Module.animalBattlerSocket.close();
    Module.animalBattlerSocket = null;
  },
  AnimalBattlerGetPageUrl: function () {
    return stringToNewUTF8(window.location.href);
  },
  AnimalBattlerCopyText: function (textPtr) {
    navigator.clipboard.writeText(UTF8ToString(textPtr));
  }
});
