## Authorization

### 치지직 Access Token 발급
```json
{
  "code":200,
  "message":null,
  "content":{
    "refreshToken":"[문자열]",
    "accessToken":"[문자열]",
    "tokenType":"Bearer",
    "expiresIn":86400,
    "scope":"채널 정보 조회 채널 관리자 조회 채팅 메시지 조회 채팅 메시지 쓰기 채팅 공지 쓰기 채팅 설정 조회 채팅 설정 변경 후원 조회 방송 설정 조회 방송 설정 변경 방송 스트림키 조회 활동제한 조회 활동제한 쓰기 구독 조회 유저 조회"
    }
}
```
### 치지직 Access Token 갱신
```json
{
  "code": 200,
  "message": null,
  "content": {
    "refreshToken": "[문자열]",
    "accessToken": "[문자열]",
    "tokenType": "Bearer",
    "expiresIn": 86400,
    "scope": "채널 정보 조회 채널 관리자 조회 채팅 메시지 조회 채팅 메시지 쓰기 채팅 공지 쓰기 채팅 설정 조회 채팅 설정 변경 후원 조회 방송 설정 조회 방송 설정 변경 방송 스트림키 조회 활동제한 조회 활동제한 쓰기 구독 조회 유저 조회"
  }
}
```
### 치지직 Access Token 삭제
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

## Session

### 세션 생성(클라이언트)
```json
{
  "code": 200,
  "message": null,
  "content": {
    "url": "[주소 문자열]"
  }
}
```

### 세션 생성(유저)
```json
{
  "code": 200,
  "message": null,
  "content": {
    "url": "[주소 문자열]"
  }
}
```

### 세션 목록 조회(클라이언트)
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": 0,
    "totalCount": 2,
    "totalPages": 1,
    "data": [
      {
        "sessionKey": "[문자열]",
        "connectedDate": "YYYY-MM-DD HH:mm:ss",
        "subscribedEvents": [
          {
            "eventType": "CHAT",
            "channelId": "[문자열]"
          }
        ]
      },
      {
        "sessionKey": "[문자열]",
        "connectedDate": "YYYY-MM-DD HH:mm:ss",
        "disconnectedDate": "YYYY-MM-DD HH:mm:ss",
        "subscribedEvents": [
          {
            "eventType": "CHAT",
            "channelId": "[문자열]"
          }
        ]
      }
    ]
  }
}
```

### 세션 목록 조회(유저)
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": 0,
    "totalCount": 27,
    "totalPages": 9,
    "data": [
      {
        "sessionKey": "[문자열]",
        "connectedDate": "YYYY-MM-DD HH:mm:ss",
        "subscribedEvents": [
          {
            "eventType": "CHAT",
            "channelId": "[문자열]"
          }
        ]
      },
      {
        "sessionKey": "[문자열]",
        "connectedDate": "YYYY-MM-DD HH:mm:ss",
        "disconnectedDate": "YYYY-MM-DD HH:mm:ss",
        "subscribedEvents": [
          {
            "eventType": "CHAT",
            "channelId": "[문자열]"
          }
        ]
      },
      {
        "sessionKey": "[문자열]",
        "connectedDate": "YYYY-MM-DD HH:mm:ss",
        "disconnectedDate": "YYYY-MM-DD HH:mm:ss",
        "subscribedEvents": [
          {
            "eventType": "CHAT",
            "channelId": "[문자열]"
          }
        ]
      }
    ]
  }
}
```

### 이벤트 구독(채팅)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 이벤트 구독 취소(채팅)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 이벤트 구독(후원)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 이벤트 구독 취소(후원)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 이벤트 구독(구독)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 이벤트 구독 취소(구독)
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 시스템 메시지
#### 연결
```json
{
  "type": "connected",
  "data": {
    "sessionKey": "[문자열]"
  }
}
```
#### 구독
```json
{
  "type": "subscribed",
  "data": {
    "eventType": "CHAT",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "subscribed",
  "data": {
    "eventType": "DONATION",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "subscribed",
  "data": {
    "eventType": "SUBSCRIPTION",
    "channelId": "[문자열]"
  }
}
```
#### 구독 취소
```json
{
  "type": "unsubscribed",
  "data": {
    "eventType": "CHAT",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "unsubscribed",
  "data": {
    "eventType": "DONATION",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "unsubscribed",
  "data": {
    "eventType": "SUBSCRIPTION",
    "channelId": "[문자열]"
  }
}
```

#### 권한 취소
```json
{
  "type": "revoked",
  "data": {
    "eventType": "CHAT",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "revoked",
  "data": {
    "eventType": "DONATION",
    "channelId": "[문자열]"
  }
}
```

```json
{
  "type": "revoked",
  "data": {
    "eventType": "SUBSCRIPTION",
    "channelId": "[문자열]"
  }
}
```

### 구독 이벤트 메시지(채팅)
```json
{
  "channelId": "[문자열]",
  "chatChannelId": "[문자열]",
  "senderChannelId": "[문자열]",
  "profile": {
    "nickname": "[문자열]",
    "verifiedMark": false,
    "badges": [
      {
        "imageUrl": "[주소 문자열]"
      }
    ],
    "userRoleCode": "streamer"
  },
  "content": "ㅁㄴㅇㅁ{:d_11:}",
  "emojis": {
    "d_11": "[주소 문자열]"
  },
  "messageTime": int64,
  "eventSentAt": "YYYY-MM-DD[T]HH:mm:ss.SSSSSSSSS"
}
```

### 구독 이벤트 메시지(후원)
```json

```
- 테스트 가능한 계정은 후원이 없어 메시지 형태는 명확히 체크 불가능...
### 구독 이벤트 메시지(구독)
```json

```
- 테스트 가능한 계정은 구독이 없어 메시지 형태는 명확히 체크 불가능...

## User

### 유저 정보 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "channelId": "[문자열]",
    "channelName": "[문자열A]",
    "nickname": "[문자열A]"
  }
}
```
- channelName과 nickname이 다른 문자로 나올 수 있는지 확인 안해봄.

## Channel

### 채널 정보 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "data": [
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]",
        "followerCount": number,
        "verifiedMark": false,
        "verifiedMarkType": null,
        "officialChannel": false
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]",
        "followerCount": number,
        "verifiedMark": true,
        "verifiedMarkType": "PARTNER",
        "officialChannel": false
      }
    ]
  }
}
```
- verifiedMarkType도 enum으로 가능할거같긴한데, 공식문서에 종류가 안적혀있어서 종류가 뭐뭐있는지 모름. 불가능.

### 채널 관리자 조회
```json{
  "code": 200,
  "message": null,
  "content": {
    "data": [
      {
        "managerChannelId": "[문자열]",
        "managerChannelName": "[문자열]",
        "userRole": "STREAMER",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      }
    ]
  }
}
```
- userRole도 사실 enum이 되어야하는데 반환 형태가 공식 문서와 다름에 각각 무엇인지 확인전에는 enum이 불가능.
### 채널 팔로워 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": 0,
    "totalCount": 7,
    "totalPages": 1,
    "data": [
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      },
      {
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss"
      }
    ]
  }
}
```

### 채널 구독자 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": 0,
    "totalCount": 0,
    "totalPages": 0,
    "data": []
  }
}
```
- 테스트 가능한 계정은 구독이 없어 구독자 형태는 명확히 체크 불가능...

## Category

### 카테고리 검색
```json
{
  "code": 200,
  "message": null,
  "content": {
    "data": [
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      },
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      },
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      },
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      },
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      },
      {
        "categoryType": "GAME",
        "categoryId": "[문자열]",
        "categoryValue": "[문자열]",
        "posterImageUrl": "[주소 문자열]"
      }
    ]
  }
}
```


## Live

### 라이브 목록 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": {
      "next": "[문자열]"
    },
    "data": [
      {
        "liveId": number,
        "liveTitle": "[문자열]",
        "liveThumbnailImageUrl": "[주소 문자열]",
        "concurrentUserCount": number,
        "openDate": "YYYY-MM-DD HH:mm:ss",
        "adult": bool,
        "tags": [],
        "categoryType": "ETC",
        "liveCategory": "[문자열]",
        "liveCategoryValue": "[문자열]",
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]"
      },
      {
        "liveId": number,
        "liveTitle": "[문자열]",
        "liveThumbnailImageUrl": "[주소 문자열]",
        "concurrentUserCount": number,
        "openDate": "YYYY-MM-DD HH:mm:ss",
        "adult": bool,
        "tags": [
          "[문자열]",
          "[문자열]",
          "[문자열]",
          "[문자열]",
          "[문자열]"
        ],
        "categoryType": "GAME",
        "liveCategory": "[문자열]",
        "liveCategoryValue": "[문자열]",
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열A]"
      },
      {
        "liveId": number,
        "liveTitle": "[문자열]",
        "liveThumbnailImageUrl": "[주소 문자열]",
        "concurrentUserCount": number,
        "openDate": "YYYY-MM-DD HH:mm:ss",
        "adult": bool,
        "tags": [
          "[문자열]",
          "[문자열]",
          "[문자열]"
        ],
        "categoryType": "ETC",
        "liveCategory": "[문자열]",
        "liveCategoryValue": "[문자열]",
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]"
      },
      {
        "liveId": number,
        "liveTitle": "[문자열]",
        "liveThumbnailImageUrl": "[주소 문자열]",
        "concurrentUserCount": number,
        "openDate": "YYYY-MM-DD HH:mm:ss",
        "adult": bool,
        "tags": [
          "[문자열]",
          "[문자열]",
          "[문자열]"
        ],
        "categoryType": "GAME",
        "liveCategory": "[문자열]",
        "liveCategoryValue": "[문자열]",
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]"
      },
      {
        "liveId": number,
        "liveTitle": "[문자열]",
        "liveThumbnailImageUrl": "[주소 문자열]",
        "concurrentUserCount": number,
        "openDate": "YYYY-MM-DD HH:mm:ss",
        "adult": bool,
        "tags": [
          "[문자열]",
          "[문자열]",
          "[문자열]",
          "[문자열]"
        ],
        "categoryType": "GAME",
        "liveCategory": "[문자열]",
        "liveCategoryValue": "[문자열]",
        "channelId": "[문자열]",
        "channelName": "[문자열]",
        "channelImageUrl": "[주소 문자열]"
      }
    ]
  }
}
```
- next로 호출한다해도 페이지에 추가되는거는 없었음
### 방송 스트림키 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "streamKey": "[문자열]"
  }
}
```

### 방송 설정 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "defaultLiveTitle": "[문자열]",
    "category": {
      "categoryType": "ETC",
      "categoryId": "[문자열]",
      "categoryValue": "[문자열]",
      "posterImageUrl": "[주소 문자열]"
    },
    "tags": [
      "[문자열]"
    ]
  }
}
```

### 방송 설정 변경
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```
- 카테고리를 변경하려면 type과 id 둘 다 넣어야하는듯.

## Chat

### 채팅 메시지 전송
```json
{
  "code": 200,
  "message": null,
  "content": {
    "messageId": "[문자열]"
  }
}
```

### 채팅 공지 등록
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```
- 어떤거로 보내도 응답 형태 똑같음.
### 채팅 설정 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "chatAvailableCondition": "NONE",
    "chatAvailableGroup": "ALL",
    "minFollowerMinute": 0,
    "allowSubscriberInFollowerMode": true,
    "chatSlowModeSec": 0,
    "chatEmojiMode": false
  }
}
```

### 채팅 설정 변경
```json
{
  "code": 200,
  "message": null,
  "content": {
    "chatAvailableCondition": "NONE",
    "chatAvailableGroup": "ALL",
    "minFollowerMinute": 0,
    "allowSubscriberInFollowerMode": true,
    "chatSlowModeSec": 3,
    "chatEmojiMode": false
  }
}
```
- 수정하고 싶은 값만 보내도 되는듯
### 채팅 메시지 숨기기
```json
```
- 권한 없음 뜸. 이유, 조건 모름. `{"code":403,"message":"FORBIDDEN"}`

## Restriction

### 활동 제한 추가
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 활동 제한 삭제
```json
{
  "code": 200,
  "message": "SUCCESS",
  "content": null
}
```

### 활동 제한 목록 조회
```json
{
  "code": 200,
  "message": null,
  "content": {
    "page": {
      "next": "VnwRxAZdVGKk0Cc+7W/AfA=="
    },
    "data": [
      {
        "restrictedChannelId": "[문자열]",
        "restrictedChannelName": "[문자열]",
        "createdDate": "YYYY-MM-DD HH:mm:ss",
        "releaseDate": null
      }
    ]
  }
}
```

### 임시제한 추가
```json
```
- 권한 없음 뜸. 이유, 조건 모름. `{"code":403,"message":"FORBIDDEN"}`
### 임시제한 해제
```json
```
- 권한 없음 뜸. 이유, 조건 모름. `{"code":403,"message":"FORBIDDEN"}`