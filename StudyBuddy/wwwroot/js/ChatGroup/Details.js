const messageArea = document.getElementById("messageArea");
const messageForm = document.getElementById("messageForm");
const messageInput = document.getElementById("messageInput");
const attachmentInput = document.getElementById("attachmentInput");
const sendButton = document.getElementById("sendButton");
const messageError = document.getElementById("messageError");
const connectionStatus = document.getElementById("connectionStatus");

const groupId = parseInt(messageArea.dataset.groupId);
const uploadUrl = messageForm.dataset.uploadUrl;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

function createReportForm(message) {
    const wrapper = document.createElement("div");
    wrapper.className = "mt-2";

    const details = document.createElement("details");

    const summary = document.createElement("summary");
    summary.className = "text-muted small";
    summary.style.cursor = "pointer";
    summary.textContent = "⚑ Report message";

    const form = document.createElement("form");
    form.method = "post";
    form.action = "/ChatGroup/ReportMessage";
    form.className = "mt-2 p-3 border rounded";

    // Add the antiforgery token
    const token = document.querySelector(
        "#antiForgeryTokenContainer input[name='__RequestVerificationToken']"
    );

    if (token) {
        const tokenInput = document.createElement("input");
        tokenInput.type = "hidden";
        tokenInput.name = "__RequestVerificationToken";
        tokenInput.value = token.value;
        form.appendChild(tokenInput);
    }

    // Identify the message and its group
    const messageIdInput = document.createElement("input");
    messageIdInput.type = "hidden";
    messageIdInput.name = "ChatMessageId";
    messageIdInput.value = message.chatMessageId;

    const groupIdInput = document.createElement("input");
    groupIdInput.type = "hidden";
    groupIdInput.name = "ChatGroupId";
    groupIdInput.value = groupId;

    form.appendChild(messageIdInput);
    form.appendChild(groupIdInput);

    // Reason selector
    const reasonGroup = document.createElement("div");
    reasonGroup.className = "mb-3";

    const reasonLabel = document.createElement("label");
    reasonLabel.className = "form-label";
    reasonLabel.textContent = "Reason for reporting";

    const reasonSelect = document.createElement("select");
    reasonSelect.name = "Reason";
    reasonSelect.className = "form-select";
    reasonSelect.required = true;

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "Select a reason...";
    placeholder.disabled = true;
    placeholder.selected = true;

    reasonSelect.appendChild(placeholder);

    const reasons = [
        { value: 0, text: "Harassment" },
        { value: 1, text: "Spam" },
        { value: 2, text: "Inappropriate Content" },
        { value: 3, text: "Threats" },
        { value: 4, text: "Other" }
    ];

    reasons.forEach(function (reason) {
        const option = document.createElement("option");
        option.value = reason.value;
        option.textContent = reason.text;
        reasonSelect.appendChild(option);
    });

    reasonGroup.appendChild(reasonLabel);
    reasonGroup.appendChild(reasonSelect);

    // Optional description
    const descriptionGroup = document.createElement("div");
    descriptionGroup.className = "mb-3";

    const descriptionLabel = document.createElement("label");
    descriptionLabel.className = "form-label";
    descriptionLabel.textContent = "Additional details (optional)";

    const description = document.createElement("textarea");
    description.name = "Description";
    description.className = "form-control";
    description.rows = 2;
    description.maxLength = 1000;
    description.placeholder = "Tell us a little more about the issue...";

    descriptionGroup.appendChild(descriptionLabel);
    descriptionGroup.appendChild(description);

    // Submit button
    const submitButton = document.createElement("button");
    submitButton.type = "submit";
    submitButton.className = "btn btn-outline-danger btn-sm";
    submitButton.textContent = "Submit Report";

    form.appendChild(reasonGroup);
    form.appendChild(descriptionGroup);
    form.appendChild(submitButton);

    details.appendChild(summary);
    details.appendChild(form);
    wrapper.appendChild(details);

    return wrapper;
}

connection.on("ReceiveMessage", function (message) {
    appendMessage(message);
});

connection.on("MessageModerationUpdated", function () {
    window.location.reload();
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

    // Message header
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
    profileImage.onerror = function () {
        this.style.display = "none";
    };

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

    messageHeader.appendChild(senderInfo);
    messageHeader.appendChild(timestamp);

    messageContainer.appendChild(messageHeader);

    // Message content
    if (message.content) {
        const content = document.createElement("div");
        content.className = "bg-light rounded p-3 mt-1";
        content.style.whiteSpace = "pre-wrap";
        content.style.overflowWrap = "anywhere";
        content.textContent = message.content;

        messageContainer.appendChild(content);
    }
  
    // Attachments
    if (message.attachments && message.attachments.length > 0) {
        const attachmentContainer = document.createElement("div");
        attachmentContainer.className = "d-flex flex-wrap gap-2 mt-2";

        message.attachments.forEach(attachment => {
            const attachmentUrl = attachment.url ||
                `/ChatGroup/DownloadAttachment/${attachment.chatAttachmentId}`;

            if (attachment.contentType.startsWith("image/")) {
                const link = document.createElement("a");
                link.href = attachmentUrl;
                link.target = "_blank";
                link.rel = "noopener noreferrer";

                const image = document.createElement("img");
                image.src = attachmentUrl;
                image.alt = attachment.fileName;
                image.className = "img-thumbnail";
                image.style.maxWidth = "180px";
                image.style.maxHeight = "180px";
                image.style.objectFit = "cover";

                link.appendChild(image);
                attachmentContainer.appendChild(link);
            }
            else {
                const link = document.createElement("a");
                link.href = attachmentUrl;
                link.target = "_blank";
                link.rel = "noopener noreferrer";
                link.className = "btn btn-outline-secondary btn-sm";

                const icon = document.createElement("i");
                icon.className = "bi bi-file-earmark-pdf me-1";

                link.appendChild(icon);
                link.appendChild(
                    document.createTextNode(attachment.fileName)
                );

                attachmentContainer.appendChild(link);
            }
        });

        messageContainer.appendChild(attachmentContainer);
    }
  
    messageContainer.appendChild(createReportForm(message));

    messageArea.appendChild(messageContainer);

    scrollToBottom();
}

// SignalR reconnection
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

        reconnectAttempts = 0;

        connectionStatus.textContent = "Connected";
        connectionStatus.className = "text-success";

        hideError();
    }
    catch (error) {
        console.error("SignalR connection error:", error);

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
        connectionStatus.textContent =
            "Disconnected - Refresh to reconnect";

        connectionStatus.className = "text-danger";

        console.log("Maximum reconnection attempts reached.");

        return;
    }

    reconnectAttempts++;

    connectionStatus.textContent =
        `Reconnecting (${reconnectAttempts}/${maxReconnectAttempts})...`;

    connectionStatus.className = "text-warning";

    reconnectTimer = setTimeout(async function () {
        reconnectTimer = null;
        await startConnection();
    }, reconnectDelay);
}

connection.onclose(function (error) {
    console.warn("SignalR connection closed:", error);
    scheduleReconnect();
});

// Send messages and attachments
messageForm.addEventListener("submit", async function (event) {
    event.preventDefault();

    const content = messageInput.value.trim();
    const files = Array.from(attachmentInput.files);

    if (!content && files.length === 0) {
        return;
    }

    if (connection.state !== signalR.HubConnectionState.Connected) {
        showError("You are not connected to the chat. Please wait.");
        return;
    }

    hideError();
    sendButton.disabled = true;

    try {
        if (files.length > 0) {
            // Upload files through HTTP.
            const formData = new FormData();

            formData.append("groupId", groupId);
            formData.append("content", content);

            files.forEach(file => {
                formData.append("attachments", file);
            });

            const token = messageForm.querySelector(
                'input[name="__RequestVerificationToken"]'
            ).value;

            formData.append("__RequestVerificationToken", token);

            const response = await fetch(uploadUrl, {
                method: "POST",
                body: formData,
                credentials: "same-origin"
            });

            const result = await response.json();

            if (!response.ok) {
                throw new Error(
                    result.error || "Unable to upload attachments."
                );
            }
        }
        else {
            // Send text-only messages through SignalR.
            await connection.invoke("SendMessage", groupId, content);
        }

        messageInput.value = "";
        attachmentInput.value = "";
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