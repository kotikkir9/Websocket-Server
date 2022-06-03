let socketOpen = false;

const button = document.querySelector("#send"),
    discButton = document.querySelector("#disconnect"),
    output = document.querySelector("#output"),
    textarea = document.querySelector("textarea"),
    wsUri = "ws://127.0.0.1/",
    websocket = new WebSocket(wsUri);

button.addEventListener("click", onClickButton);
discButton.addEventListener("click", () => {
    websocket.close();
});

websocket.onopen = function (e) {
    socketOpen = true;
    writeToScreen("CONNECTED");
    doSend("Hello");
};

websocket.onclose = function (e) {
    socketOpen = false;
    writeToScreen("DISCONNECTED");
};

websocket.onmessage = function (e) {
    writeToScreen("<span>RESPONSE: " + e.data + "</span>");
};

websocket.onerror = function (e) {
    console.log(e);
    writeToScreen("<span class=error>ERROR:</span> " + e.data);
};

function doSend(message) {
    if (socketOpen) {
        writeToScreen("SENT: " + message);
        websocket.send(message);
    } else {
        writeToScreen("Connection closed!");
    }
}

function writeToScreen(message) {
    output.insertAdjacentHTML(
        "afterbegin",
        "<p>" + message + "</p>"
    );
}

function onClickButton() {
    var text = textarea.value;

    text && doSend(text);
    textarea.value = "";
    textarea.focus();
}