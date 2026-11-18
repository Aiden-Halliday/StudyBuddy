    const messageArea = document.getElementById("messageArea");
    const messageForm = document.getElementById("messageForm");
    const messageInput = document.getElementById("messageInput");
    const sendButton = document.getElementById("sendButton");
    const messageError = document.getElementById("messageError");
    const connectionStatus = document.getElementById("connectionStatus");

    const groupId = parseInt(messageArea.dataset.groupId);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .build();

    connection.on("ReceiveMessage", function (message) {
        appendMessage(message);
    });

    function scrollToBottom() {
        messageArea.scrollTop = messageArea.scrollHeight;
    }

    function showError(message) {
        messageError.textContent = message;
        messageError.classList.remove("d-none");
    }

    function hideError() {
        messageError.textContent = "";
        messageError.classList.add("d-none");
    }

    function appendMessage(message) {
        const emptyMessage = document.getElementById("emptyMessage");

        if (emptyMessage) {
            emptyMessage.remove();
        }

        const messageContainer = document.createElement("div");
        messageContainer.className = "mb-3";

        const messageHeader = document.createElement("div");
        messageHeader.className = "d-flex justify-content-between align-items-center";

        
        const senderInfo = document.createElement("div");
        senderInfo.className = "d-flex align-items-center gap-2";

        
        const profileImage = document.createElement("img");
        profileImage.src = message.profileImageUrl;
        profileImage.alt = message.senderName;
        profileImage.className = "rounded-circle";
        profileImage.style.width = "35px";
        profileImage.style.height = "35px";
        profileImage.style.objectFit = "cover";

        const sender = document.createElement("strong");
        sender.textContent = message.senderName;

        senderInfo.appendChild(profileImage);
        senderInfo.appendChild(sender);

        const timestamp = document.createElement("small");
        timestamp.className = "text-muted";

        const sentDate = new Date(message.sentUtc);

        timestamp.textContent = sentDate.toLocaleString([], {
            month: "short",
            day: "2-digit",
            hour: "numeric",
            minute: "2-digit"
        });

        // Message content
        const content = document.createElement("div");
        content.className = "bg-light rounded p-3 mt-1";
        content.style.whiteSpace = "pre-wrap";
        content.style.overflowWrap = "anywhere";
        content.textContent = message.content;

        messageHeader.appendChild(senderInfo);
        messageHeader.appendChild(timestamp);

        messageContainer.appendChild(messageHeader);
        messageContainer.appendChild(content);

        messageArea.appendChild(messageContainer);

        scrollToBottom();
    }

    const maxReconnectAttempts = 5;
    const reconnectDelay = 5000;

    let reconnectAttempts = 0;
    let reconnectTimer = null;

    async function startConnection() {
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            await connection.start();

            console.log("SignalR connected.");

            await connection.invoke("JoinGroup", groupId);

            console.log("Joined chat group:", groupId);

            // Reset attempts after a successful connection.
            reconnectAttempts = 0;

            connectionStatus.textContent = "Connected";
            connectionStatus.className = "text-success";

            hideError();
        } 
        catch (error) {
            console.error("SignalR connection error:", error);

            // If the connection started but joining failed, stop it.
            if (connection.state !== signalR.HubConnectionState.Disconnected) {
                await connection.stop();
            }

            scheduleReconnect();
        }
    }

    function scheduleReconnect() {
        if (reconnectTimer !== null) {
            return;
        }

        if (reconnectAttempts >= maxReconnectAttempts) {
            connectionStatus.textContent = "Disconnected - Refresh to reconnect";
            connectionStatus.className = "text-danger";

            console.log("Maximum reconnection attempts reached.");

            return;
        }

        reconnectAttempts++;

        connectionStatus.textContent = `Reconnecting (${reconnectAttempts}/${maxReconnectAttempts})...`;

        connectionStatus.className = "text-warning";

        console.log(`Reconnection attempt ${reconnectAttempts}/${maxReconnectAttempts} in 5 seconds.`);

        reconnectTimer = setTimeout(async function () {
            reconnectTimer = null;
            await startConnection();
        }, reconnectDelay);
    }

    connection.onclose(function (error) {
        console.warn("SignalR connection closed:", error);

        scheduleReconnect();
    });

    // Send a message through SignalR.
    messageForm.addEventListener("submit", async function (event) {
        event.preventDefault();

        const content = messageInput.value.trim();

        if (!content) {
            return;
        }

        if (connection.state !== signalR.HubConnectionState.Connected) {
            showError("You are not connected to the chat. Please wait.");
            return;
        }

        hideError();
        sendButton.disabled = true;

        try {
            await connection.invoke("SendMessage", groupId, content);

            messageInput.value = "";
            messageInput.focus();
        }
        catch (error) {
            console.error(error);
            showError(error.message || "Unable to send message.");
        }
        finally {
            sendButton.disabled = false;
        }
    });

    scrollToBottom();
    startConnection();