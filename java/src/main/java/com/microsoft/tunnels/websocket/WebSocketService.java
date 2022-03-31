package com.microsoft.tunnels.websocket;

import java.util.Map;
import java.util.Objects;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;

import io.netty.channel.group.ChannelGroup;
import io.netty.util.AttributeKey;
import org.apache.sshd.common.AttributeRepository;
import org.apache.sshd.common.io.IoConnectFuture;
import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoService;
import org.apache.sshd.common.io.IoServiceEventListener;
import org.apache.sshd.common.io.IoSession;
import org.apache.sshd.common.util.closeable.AbstractCloseable;

public abstract class WebSocketService extends AbstractCloseable implements IoService {

  public static final AttributeKey<IoConnectFuture> CONNECT_FUTURE_KEY = AttributeKey
      .valueOf(IoConnectFuture.class.getName());
  public static final AttributeKey<AttributeRepository> CONTEXT_KEY = AttributeKey
      .valueOf(AttributeRepository.class.getName());

  protected final AtomicLong sessionSeq = new AtomicLong();
  protected final Map<Long, IoSession> sessions = new ConcurrentHashMap<>();
  protected ChannelGroup channelGroup;
  protected final WebSocketServiceFactory factory;
  protected final IoHandler handler;

  private IoServiceEventListener eventListener;

  protected WebSocketService(WebSocketServiceFactory factory, IoHandler handler) {
    this.factory = Objects.requireNonNull(factory, "No factory instance provided");
    this.handler = Objects.requireNonNull(handler, "No I/O handler provied");
    this.eventListener = factory.getIoServiceEventListener();
  }

  @Override
  public IoServiceEventListener getIoServiceEventListener() {
    return eventListener;
  }

  @Override
  public void setIoServiceEventListener(IoServiceEventListener listener) {
    eventListener = listener;
  }

  @Override
  public Map<Long, IoSession> getManagedSessions() {
    return sessions;
  }
}
