using Microsoft.AspNetCore.Mvc;
using NoteApi.Models;
using NoteApi.Services;
using Supabase;
using Supabase.Postgrest.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoteApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatMessengerController(Client supabase, FcmNotificationService fcmService) : ControllerBase
    {
        private readonly Client _supabase = supabase;
        private readonly FcmNotificationService _fcmService = fcmService;

        private static int ParseContentRangeCount(string? contentRange)
        {
            if (string.IsNullOrEmpty(contentRange))
                return 0;

            var parts = contentRange.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[1], out var count))
                return count;

            return 0;
        }

        private static ChatMessengerDto MapToDto(ChatMessenger message)
        {
            return new ChatMessengerDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                MessageType = message.MessageType,
                IsRead = message.IsRead,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt
            };
        }

        private async Task<bool> UserExists(string senderId)
        {
            var user = await _supabase.From<UserProfile>().Where(u => u.Id == senderId).Single();
            return user != null;
        }

        [HttpGet]
        public async Task<ActionResult<PageChatMessengerResult>> GetMessages([FromQuery] ChatMessengerQueryParams query)
        {
            query ??= new ChatMessengerQueryParams();
            var page = query.Page > 0 ? query.Page : 1;
            var pageSize = query.PageSize > 0 ? query.PageSize : 0;

            List<ChatMessenger> allMessages;
            if (!string.IsNullOrWhiteSpace(query.SenderId) && !string.IsNullOrWhiteSpace(query.ReceiverId))
            {
                var q1 = (IPostgrestTable<ChatMessenger>)_supabase.From<ChatMessenger>();
                q1 = q1.Where(m => m.SenderId == query.SenderId && m.ReceiverId == query.ReceiverId);
                if (query.IsRead.HasValue) q1 = q1.Where(m => m.IsRead == query.IsRead.Value);
                if (!string.IsNullOrWhiteSpace(query.Search)) q1 = q1.Where(m => m.Content!.Contains(query.Search!.Trim()));
                var q2 = (IPostgrestTable<ChatMessenger>)_supabase.From<ChatMessenger>();
                q2 = q2.Where(m => m.SenderId == query.ReceiverId && m.ReceiverId == query.SenderId);
                if (query.IsRead.HasValue) q2 = q2.Where(m => m.IsRead == query.IsRead.Value);
                if (!string.IsNullOrWhiteSpace(query.Search)) q2 = q2.Where(m => m.Content!.Contains(query.Search!.Trim()));

                var r1Task = q1.Get();
                var r2Task = q2.Get();
                await Task.WhenAll(r1Task, r2Task);

                var r1 = r1Task.Result;
                var r2 = r2Task.Result;

                allMessages = r1.Models
                    .Concat(r2.Models)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();
            }
            else
            {
                var baseQuery = (IPostgrestTable<ChatMessenger>)_supabase.From<ChatMessenger>();

                if (query.ConversationId.HasValue)
                    baseQuery = baseQuery.Where(m => m.ConversationId == query.ConversationId.Value);

                if (!string.IsNullOrWhiteSpace(query.SenderId))
                    baseQuery = baseQuery.Where(m => m.SenderId == query.SenderId);

                if (!string.IsNullOrWhiteSpace(query.ReceiverId))
                    baseQuery = baseQuery.Where(m => m.ReceiverId == query.ReceiverId);

                if (query.IsRead.HasValue)
                    baseQuery = baseQuery.Where(m => m.IsRead == query.IsRead.Value);

                if (!string.IsNullOrWhiteSpace(query.Search))
                    baseQuery = baseQuery.Where(m => m.Content!.Contains(query.Search.Trim()));

                var response = await baseQuery.Order("CreatedAt", Supabase.Postgrest.Constants.Ordering.Ascending).Get();
                allMessages = response.Models;
            }

            var totalCount = allMessages.Count;
            var pagedItems = pageSize > 0
                ? allMessages.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                : allMessages;

            return Ok(new PageChatMessengerResult
            {
                Items = pagedItems.Select(MapToDto).ToList(),
                Page = pageSize > 0 ? page : null,
                PageSize = pageSize > 0 ? pageSize : null,
                TotalCount = totalCount
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChatMessengerDto>> GetMessage(int id)
        {
            var response = await _supabase.From<ChatMessenger>().Where(m => m.Id == id).Single();

            if (response == null)
            {
                return NotFound(new { error = "Message not found" });
            }

            return Ok(MapToDto(response));
        }

        [HttpGet("conversation/{conversationId:int}")]
        public async Task<ActionResult<IEnumerable<ChatMessengerDto>>> GetMessagesByConversation(int conversationId)
        {
            var response = await _supabase
                .From<ChatMessenger>()
                .Where(m => m.ConversationId == conversationId)
                .Order("CreatedAt", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            return Ok(response.Models.Select(MapToDto).ToList());
        }

        [HttpGet("unread-count/{receiverId}")]
        public async Task<ActionResult<object>> GetUnreadCount(string receiverId)
        {
            if (string.IsNullOrWhiteSpace(receiverId))
                return BadRequest(new { error = "ReceiverId is required" });

            var response = await _supabase
                .From<ChatMessenger>()
                .Where(m => m.ReceiverId == receiverId && m.IsRead == false)
                .Get();

            return Ok(new { receiverId, unreadCount = response.Models.Count });
        }

        [HttpPost]
        public async Task<ActionResult<ChatMessengerDto>> CreateMessage([FromBody] ChatMessengerCreateDto messageDto)
        {

            if (string.IsNullOrWhiteSpace(messageDto.SenderId))
            {
                return BadRequest(new { error = "SenderId is required and must match a user profile id" });
            }

            if (!await UserExists(messageDto.SenderId))
            {
                return BadRequest(new { error = "SenderId does not exist in userinfo" });
            }

            if (!string.IsNullOrWhiteSpace(messageDto.ReceiverId) && !await UserExists(messageDto.ReceiverId))
            {
                return BadRequest(new { error = "ReceiverId does not exist in userinfo" });
            }

            var message = new ChatMessenger
            {
                SenderId = messageDto.SenderId,
                ReceiverId = messageDto.ReceiverId,
                Content = messageDto.Content,
                MessageType = string.IsNullOrWhiteSpace(messageDto.MessageType) ? "text" : messageDto.MessageType,
                IsRead = messageDto.IsRead ?? false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var response = await _supabase
                .From<ChatMessenger>()
                .Insert(message, new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation
                });

            var createdMessage = response.Models.FirstOrDefault();
            if (createdMessage == null)
            {
                return BadRequest(new { error = "Failed to create message" });
            }
            var updatePayload = new ChatMessenger
            {
                Id = createdMessage.Id,
                ConversationId = createdMessage.Id,
                SenderId = createdMessage.SenderId,
                ReceiverId = createdMessage.ReceiverId,
                Content = createdMessage.Content,
                MessageType = createdMessage.MessageType,
                IsRead = createdMessage.IsRead,
                CreatedAt = createdMessage.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _supabase
                .From<ChatMessenger>()
                .Where(m => m.Id == createdMessage.Id)
                .Update(updatePayload);

            createdMessage.ConversationId = createdMessage.Id;

            // Send push notification to receiver — wrapped in try/catch so it never crashes message creation
            if (!string.IsNullOrWhiteSpace(messageDto.ReceiverId))
            {
                try
                {
                    var deviceResponse = await _supabase
                        .From<UserDevice>()
                        .Where(d => d.UserId == messageDto.ReceiverId)
                        .Single();

                    if (deviceResponse?.FCMToken != null)
                    {
                        await _fcmService.SendAsync(
                            deviceResponse.FCMToken,
                            title: "New Message",
                            body: messageDto.Content ?? "You received a new message",
                            data: new { conversationId = createdMessage.ConversationId.ToString() }
                        );
                    }
                }
                catch
                {
                    // Notification failure should not fail the message creation
                }
            }

            return CreatedAtAction(nameof(GetMessage), new { id = createdMessage.Id }, MapToDto(createdMessage));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChatMessengerDto>> UpdateMessage(int id, [FromBody] ChatMessengerUpdateDto messageDto)
        {
            var existingMessage = await _supabase.From<ChatMessenger>().Where(m => m.Id == id).Single();

            if (existingMessage == null)
            {
                return NotFound(new { error = "Message not found" });
            }

            if (!string.IsNullOrWhiteSpace(messageDto.SenderId) && !await UserExists(messageDto.SenderId))
            {
                return BadRequest(new { error = "SenderId does not exist in userinfo" });
            }

            if (!string.IsNullOrWhiteSpace(messageDto.ReceiverId) && !await UserExists(messageDto.ReceiverId))
            {
                return BadRequest(new { error = "ReceiverId does not exist in userinfo" });
            }

            existingMessage.SenderId = messageDto.SenderId ?? existingMessage.SenderId;
            existingMessage.ReceiverId = messageDto.ReceiverId ?? existingMessage.ReceiverId;
            existingMessage.Content = messageDto.Content ?? existingMessage.Content;
            existingMessage.MessageType = messageDto.MessageType ?? existingMessage.MessageType;
            existingMessage.IsRead = messageDto.IsRead ?? existingMessage.IsRead;
            existingMessage.UpdatedAt = DateTime.UtcNow;

            await _supabase
                .From<ChatMessenger>()
                .Where(m => m.Id == id)
                .Update(existingMessage);

            return Ok(MapToDto(existingMessage));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var existingMessage = await _supabase.From<ChatMessenger>().Where(m => m.Id == id).Single();

            if (existingMessage == null)
            {
                return NotFound(new { error = "Message not found" });
            }

            await _supabase
                .From<ChatMessenger>()
                .Where(m => m.Id == id)
                .Delete();

            return NoContent();
        }

        [HttpPut("{id}/mark-as-read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var existingMessage = await _supabase.From<ChatMessenger>().Where(m => m.Id == id).Single();

            if (existingMessage == null)
                return NotFound(new { error = "Message not found" });

            existingMessage.IsRead = true;
            existingMessage.UpdatedAt = DateTime.UtcNow;

            await _supabase
                .From<ChatMessenger>()
                .Where(m => m.Id == id)
                .Update(existingMessage);

            return Ok(new { message = "Message marked as read", id = id });
        }
    }
}
